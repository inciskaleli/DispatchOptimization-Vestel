namespace Algorithm;

public class BusinessUnit
{
    public String MasterBusinessUnitID; 
    public String BusinessUnitID;
    public Int32 BufferSlotCount;
    public Double BufferSlotDurationInMinutes;
    public List<String> EligibleTechnicians;

    readonly Optimizer Optimizer;
    public Double ExpectedRevenue
    {
        get
        {
            var TaskListInSameBusinessUnit = Optimizer.Tasks.Where(t=>t.Value.BusinessUnitID == BusinessUnitID).Select(t=>t.Key).ToList();
            if (TaskListInSameBusinessUnit.Count == 0)
                return 0;
            else
                return Optimizer.Tasks.Where(t=>TaskListInSameBusinessUnit.Contains(t.Key)).Select(t => t.Value.MaximumRevenue).Average();
        }
    }

    public Double ExpectedRevenuePerEligibleTechnician (string eligbleTech)
    {
        if (!EligibleTechnicians.Contains(eligbleTech))
            return 0;
        else
        {
            return Optimizer.BusinessUnits.Where(b=>b.Value.EligibleTechnicians.Contains (eligbleTech)).Select(b=>b.Value.ExpectedRevenue).Max();
        }
    }
    public BusinessUnit(Optimizer optimizer, String mkey, String key, int slotCount, double slotLength)
    {
        Optimizer = optimizer;
        MasterBusinessUnitID = mkey;
        BusinessUnitID = key;
        BufferSlotCount = slotCount;
        BufferSlotDurationInMinutes = slotLength/60;
        EligibleTechnicians = new List<string>();
    }

}