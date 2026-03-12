namespace GameCore.Content.Raw
{
    /// <summary>Raw skill data as parsed directly from YAML. No validation or compilation yet.</summary>
    public class RawSkill
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Cost { get; set; }
        public double DamageMultiplier { get; set; } = 1.0;
        public bool IsAoe { get; set; }
        public string Target { get; set; } = "Enemy";
        public string Kind { get; set; } = "Damage";
        public int Cooldown { get; set; }
        public int InitialCooldown { get; set; }
        public string EffectType { get; set; } = "Physical";
    }
}
