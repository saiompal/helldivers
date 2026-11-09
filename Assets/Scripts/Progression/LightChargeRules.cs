using System;

public static class LightChargeRules
{
    public static int CapStoredStartingCharges(int charges)
    {
        if (charges < 0)
            return 0;
        return Math.Min(charges, PlayerProgress.MaxStoredStartingCharges);
    }

    // The five-charge limit applies only to permanent starting allowance.
    // Star rewards are live run currency and intentionally have no gameplay cap.
    public static int AddStarReward(int currentCharges)
    {
        if (currentCharges < 0)
            currentCharges = 0;
        return currentCharges == int.MaxValue ? int.MaxValue : currentCharges + 1;
    }
}
