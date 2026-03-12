using System;
using System.Collections.Generic;
using System.Linq;
namespace GameCore.Battle
{
    /// <summary>
    /// The concrete implementation of <see cref="IBattleEngine"/>.
    /// <para>
    /// Instantiate with a seed, call <see cref="Start"/> once with the <see cref="BattleSetup"/>,
    /// then drive the battle with <see cref="TryExecute"/>.
    /// </para>
    /// <para>
    /// All state-changing commands are validated before any state change occurs.
    /// Invalid commands return a rejected <see cref="BattleStepResult"/> and leave state unchanged.
    /// </para>
    /// </summary>
    public sealed class BattleSession : IBattleEngine
    {
        private readonly int _seed;
        private BattleSetup? _setup;
        private InteractiveBattleSession? _inner;
        private BattleResponse? _lastResponse;

        public BattleSession(int seed) => _seed = seed;

        // ── IBattleEngine ────────────────────────────────────────────────────

        public BattleStartResult Start(BattleSetup setup)
        {
            if (_inner != null)
            {
                return new BattleStartResult(
                    false,
                    new ValidationError(ValidationErrorCode.SessionAlreadyStarted, "Session already started."),
                    BuildView(_lastResponse!),
                    Array.Empty<BattleEvent>()
                );
            }

            _setup = setup;
            _inner = new InteractiveBattleSession(setup, _seed);
            _lastResponse = _inner.HandleRequest(new InitiateBattleRequest());
            return new BattleStartResult(true, null, BuildView(_lastResponse), _lastResponse.NewEvents);
        }

        public BattleStepResult TryExecute(BattleCommand command)
        {
            if (_inner == null || _lastResponse == null)
                return Rejected(ValidationErrorCode.BattleNotStarted, "Call Start() before executing commands.");

            return command switch
            {
                PlayerActionCommand cmd => ExecutePlayerAction(cmd),
                AutoPlayerActionCommand => ExecuteAutoPlayerAction(),
                AdvanceTurnCommand => ExecuteAdvanceTurn(),
                ResumeFromSnapshotCommand cmd => ExecuteResume(cmd),
                _ => throw new ArgumentException($"Unknown command type: {command.GetType().Name}"),
            };
        }

        public BattleView GetView()
        {
            if (_lastResponse == null)
                throw new InvalidOperationException("Call Start() before calling GetView().");
            return BuildView(_lastResponse);
        }

        /// <summary>
        /// Runs a complete battle with AI controlling all units and returns a <see cref="BattleResult"/>
        /// with one snapshot per event — suitable for watch-mode replay.
        /// This is the single authoritative execution path; <see cref="BattleEngine"/> delegates here.
        /// </summary>
        public static BattleResult RunFull(BattleSetup setup, int seed)
        {
            var session = new BattleSession(seed);
            var snapshots = new List<BattleSnapshot>();
            int step = 0;
            int logOffset = 0;

            var startResult = session.Start(setup);
            // Use FullLog to capture the "start" event, which is added to the log inside
            // InteractiveBattleSession.HandleStart() but is NOT included in NewEvents.
            var initialStates = startResult.View.Units;
            foreach (var ev in startResult.View.FullLog)
                snapshots.Add(new BattleSnapshot { Step = step++, Event = ev, UnitStates = initialStates });
            logOffset = startResult.View.FullLog.Count;

            while (!session.GetView().IsOver)
            {
                var result = session.TryExecute(new AdvanceTurnCommand());
                var unitStates = result.View.Units;
                // Slice new events from FullLog so we capture the "end" event produced by
                // CheckEnd(), which is also not in NewEvents.
                var fullLog = result.View.FullLog;
                for (int i = logOffset; i < fullLog.Count; i++)
                    snapshots.Add(new BattleSnapshot { Step = step++, Event = fullLog[i], UnitStates = unitStates });
                logOffset = fullLog.Count;
            }

            var view = session.GetView();
            return new BattleResult
            {
                Snapshots = snapshots,
                WinningTeam = view.WinningTeam ?? "enemy",
                Seed = seed,
            };
        }

        // ── Command handlers ─────────────────────────────────────────────────

        private BattleStepResult ExecutePlayerAction(PlayerActionCommand cmd)
        {
            if (_lastResponse!.IsOver)
                return Rejected(ValidationErrorCode.BattleAlreadyOver, "The battle has ended.");

            if (_lastResponse.PendingInput == null)
                return Rejected(ValidationErrorCode.NotPlayerTurn, "It is not the player's turn.");

            var pending = _lastResponse.PendingInput;

            // Validate skill exists on the actor.
            var skill = pending.Skills.FirstOrDefault(s => s.Id == cmd.SkillId);
            if (skill == null)
                return Rejected(ValidationErrorCode.UnknownSkill, $"Unknown skill: '{cmd.SkillId}'.");

            // Validate the skill is usable (MP and cooldown).
            if (!pending.AvailableSkillIds.Contains(cmd.SkillId))
            {
                int cd = pending.SkillCooldowns.TryGetValue(cmd.SkillId, out var c) ? c : 0;
                return cd > 0
                    ? Rejected(
                        ValidationErrorCode.SkillOnCooldown,
                        $"Skill '{cmd.SkillId}' is on cooldown ({cd} turn(s) remaining)."
                    )
                    : Rejected(
                        ValidationErrorCode.InsufficientMp,
                        $"Not enough MP to use '{cmd.SkillId}'."
                    );
            }

            // Validate target for single-target skills.
            if (!skill.IsAoe)
            {
                if (cmd.TargetId == null)
                    return Rejected(
                        ValidationErrorCode.InvalidTarget,
                        "A target must be specified for single-target skills."
                    );

                var validTargets =
                    skill.Target == BattleSkillTarget.Ally
                        ? pending.AllyTargets
                        : pending.EnemyTargets;

                if (!validTargets.Any(u => u.Id == cmd.TargetId))
                    return Rejected(
                        ValidationErrorCode.InvalidTarget,
                        $"'{cmd.TargetId}' is not a valid living target for skill '{cmd.SkillId}'."
                    );
            }

            _lastResponse = _inner!.HandleRequest(new PlayerActionRequest(cmd.SkillId, cmd.TargetId));
            return Accepted(_lastResponse);
        }

        private BattleStepResult ExecuteAutoPlayerAction()
        {
            if (_lastResponse!.IsOver)
                return Rejected(ValidationErrorCode.BattleAlreadyOver, "The battle has ended.");

            if (_lastResponse.PendingInput == null)
                return Rejected(ValidationErrorCode.NotPlayerTurn, "It is not the player's turn.");

            _lastResponse = _inner!.HandleRequest(new AutoPlayerActionRequest());
            return Accepted(_lastResponse);
        }

        private BattleStepResult ExecuteAdvanceTurn()
        {
            // Advancing when already over is a no-op, not an error.
            if (_lastResponse!.IsOver)
                return new BattleStepResult(true, null, BuildView(_lastResponse), Array.Empty<BattleEvent>());

            _lastResponse = _inner!.HandleRequest(new AdvanceOneTurnRequest());
            return Accepted(_lastResponse);
        }

        private BattleStepResult ExecuteResume(ResumeFromSnapshotCommand cmd)
        {
            if (_setup == null)
                return Rejected(ValidationErrorCode.BattleNotStarted, "Call Start() before resuming from a snapshot.");

            // Recreate the inner session from scratch and apply the snapshot state.
            // This is what "take control from here" requires: fresh deterministic session
            // with state overridden from a known snapshot point.
            _inner = new InteractiveBattleSession(_setup, _seed);
            _lastResponse = _inner.HandleRequest(
                new ResumeFromSnapshotRequest(cmd.State, cmd.LastActorId, cmd.AtStep)
            );
            return Accepted(_lastResponse);
        }

        // ── Result builders ──────────────────────────────────────────────────

        private BattleStepResult Accepted(BattleResponse response) =>
            new BattleStepResult(true, null, BuildView(response), response.NewEvents);

        private static readonly BattleView EmptyView = new(
            Units: Array.Empty<UnitState>(),
            PendingInput: null,
            FullLog: Array.Empty<BattleEvent>(),
            IsOver: false,
            WinningTeam: null
        );

        private BattleStepResult Rejected(ValidationErrorCode code, string message) =>
            new BattleStepResult(
                false,
                new ValidationError(code, message),
                _lastResponse != null ? BuildView(_lastResponse) : EmptyView,
                Array.Empty<BattleEvent>()
            );

        // ── View projection ──────────────────────────────────────────────────

        private BattleView BuildView(BattleResponse response)
        {
            PendingInputView? inputView = null;
            if (response.PendingInput is BattlePendingInput p)
            {
                inputView = new PendingInputView(
                    ActorId: p.Actor.Id,
                    ActorName: p.Actor.Name,
                    Skills: p.Skills.Select(s => ToSkillView(s, p.Actor)).ToArray(),
                    AvailableSkillIds: p.AvailableSkillIds,
                    EnemyTargetIds: p.EnemyTargets.Select(u => u.Id).ToArray(),
                    AllyTargetIds: p.AllyTargets.Select(u => u.Id).ToArray(),
                    SkillCooldowns: p.SkillCooldowns
                );
            }

            return new BattleView(
                Units: response.State,
                PendingInput: inputView,
                FullLog: response.FullLog,
                IsOver: response.IsOver,
                WinningTeam: response.WinningTeam,
                Round: response.Round
            );
        }

        private static SkillView ToSkillView(BattleSkill s, BattleUnit actor) =>
            new SkillView(s.Id, s.Name, s.Cost, s.DamageMultiplier, s.IsAoe, s.IsHeal, s.Cooldown, s.Target,
                s.EffectType, s.IsBasic, s.IsUltimate, s.EffectiveInitialCooldown,
                (int)(actor.GetBaseAttack(s.EffectType) * s.DamageMultiplier));
    }
}
