namespace Research
{
    public enum ResearchTier
    {
        Binary,
        Tier10,
        Tier25,
        Tier100Standard,
        Tier100Absurd
    }

    public static class ResearchTierExtensions
    {
        public static int MaxLevel(this ResearchTier tier) => tier switch
        {
            ResearchTier.Binary => 1,
            ResearchTier.Tier10 => 10,
            ResearchTier.Tier25 => 25,
            ResearchTier.Tier100Standard => 100,
            ResearchTier.Tier100Absurd => 100,
            _ => 1
        };
    }
}
