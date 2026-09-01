namespace RaqmiSystem.Domain.Kpi;

/// <summary>
/// Les codes stables des indicateurs. Un code est l'IDENTITE d'un indicateur : il voyage dans
/// les URL (<c>/api/v1/kpis/{code}</c>), dans les instantanes historises et dans les seuils
/// configures par l'etablissement. Il ne change jamais - renommer un indicateur se fait sur son
/// libelle, jamais sur son code, sinon tout l'historique deja pose devient orphelin.
///
/// Convention : MAJUSCULES et underscores, prefixe par le domaine quand il leve une ambiguite
/// (<c>FB_</c> pour la restauration, <c>HR_</c> pour les ressources humaines).
/// </summary>
public static class KpiCodes
{
    // ------------------------------------------------------------------ Hebergement
    public const string OccupancyRate = "OCCUPANCY_RATE";
    public const string RoomsAvailable = "ROOMS_AVAILABLE";
    public const string RoomsOccupied = "ROOMS_OCCUPIED";
    public const string RoomsSold = "ROOMS_SOLD";
    public const string RoomsOutOfOrder = "ROOMS_OUT_OF_ORDER";
    public const string PhysicalRooms = "PHYSICAL_ROOMS";
    public const string ComplimentaryRooms = "COMPLIMENTARY_ROOMS";
    public const string Adr = "ADR";
    public const string RevPar = "REVPAR";
    public const string TRevPar = "TREVPAR";
    public const string GopPar = "GOPPAR";
    public const string Alos = "ALOS";
    public const string CancellationRate = "CANCELLATION_RATE";
    public const string NoShowRate = "NOSHOW_RATE";
    public const string NoShowLostRevenue = "NOSHOW_LOST_REVENUE";
    public const string GuestNights = "GUEST_NIGHTS";
    public const string RevenuePerGuest = "REVENUE_PER_GUEST";
    public const string BookingLeadTime = "BOOKING_LEAD_TIME";
    public const string Cpor = "CPOR";

    // ---------------------------------------------------------------------- Finance
    public const string RevenueTotal = "REVENUE_TOTAL";
    public const string RevenueAccommodation = "REVENUE_ACCOMMODATION";
    public const string RevenueFood = "REVENUE_FOOD";
    public const string RevenueBeverage = "REVENUE_BEVERAGE";
    public const string RevenueOther = "REVENUE_OTHER";
    public const string RevenueBudgetVariance = "REVENUE_BUDGET_VARIANCE";
    public const string RevenueBudgetAchievement = "REVENUE_BUDGET_ACHIEVEMENT";
    public const string GrossOperatingProfit = "GOP";
    public const string Ebitda = "EBITDA";
    public const string GrossMarginRate = "GROSS_MARGIN_RATE";
    public const string OperatingMarginRate = "OPERATING_MARGIN_RATE";
    public const string CashIn = "CASH_IN";
    public const string CashOut = "CASH_OUT";
    public const string OperatingCashFlow = "OPERATING_CASH_FLOW";
    public const string CashBalance = "CASH_BALANCE";
    public const string CommittedOutflow7D = "COMMITTED_OUTFLOW_7D";
    public const string CommittedOutflow30D = "COMMITTED_OUTFLOW_30D";
    public const string CommittedOutflow90D = "COMMITTED_OUTFLOW_90D";
    public const string Dso = "DSO";
    public const string ReceivablesTotal = "RECEIVABLES_TOTAL";
    public const string ReceivablesOver90 = "RECEIVABLES_OVER_90D";
    public const string ReceivablesOverdueRate = "RECEIVABLES_OVERDUE_RATE";

    // ------------------------------------------------------------------ Restauration
    public const string FoodCostRate = "FB_FOOD_COST_RATE";
    public const string FoodCostAmount = "FB_FOOD_COST_AMOUNT";
    public const string BeverageCostRate = "FB_BEVERAGE_COST_RATE";
    public const string BeverageCostAmount = "FB_BEVERAGE_COST_AMOUNT";
    public const string TotalCostOfSalesRate = "FB_COST_OF_SALES_RATE";
    public const string TheoreticalFoodCostRate = "FB_THEORETICAL_FOOD_COST_RATE";
    public const string FoodCostVariance = "FB_FOOD_COST_VARIANCE";
    public const string AverageCheck = "FB_AVERAGE_CHECK";
    public const string RevPash = "FB_REVPASH";
    public const string CostPerCover = "FB_COST_PER_COVER";
    public const string WasteCost = "FB_WASTE_COST";
    public const string WasteRate = "FB_WASTE_RATE";

    // ------------------------------------------------------------ Ressources humaines
    public const string PayrollToRevenueRate = "HR_PAYROLL_TO_REVENUE";
    public const string PayrollCost = "HR_PAYROLL_COST";
    public const string PayrollCostPerEmployee = "HR_COST_PER_EMPLOYEE";
    public const string PayrollCostPerAvailableRoom = "HR_COST_PER_AVAILABLE_ROOM";
    public const string PayrollCostPerOccupiedRoom = "HR_COST_PER_OCCUPIED_ROOM";
    public const string AbsenteeismRate = "HR_ABSENTEEISM_RATE";
    public const string TurnoverRate = "HR_TURNOVER_RATE";
    public const string HeadcountAverage = "HR_HEADCOUNT_AVERAGE";
    public const string OvertimeRate = "HR_OVERTIME_RATE";
    public const string RevenuePerEmployee = "HR_REVENUE_PER_EMPLOYEE";
    public const string RevenuePerWorkedHour = "HR_REVENUE_PER_WORKED_HOUR";
    public const string RoomsCleanedPerAttendant = "HR_ROOMS_PER_ATTENDANT";
    public const string CoversPerWaiter = "HR_COVERS_PER_WAITER";
    public const string InterventionsPerTechnician = "HR_INTERVENTIONS_PER_TECHNICIAN";

    // -------------------------------------------------------------------- Maintenance
    public const string Mttr = "MTTR";
    public const string Mtbf = "MTBF";
    public const string PreventiveCompletionRate = "PREVENTIVE_COMPLETION_RATE";
    public const string MaintenanceCostPerEquipment = "MAINTENANCE_COST_PER_EQUIPMENT";
    public const string MaintenanceCostToAssetValue = "MAINTENANCE_COST_TO_ASSET_VALUE";

    // ------------------------------------------------------------- Experience client
    public const string GuestSatisfactionScore = "GUEST_SATISFACTION_SCORE";
    public const string Nps = "NPS";
    public const string RepeatGuestRate = "REPEAT_GUEST_RATE";
    public const string ComplaintRate = "COMPLAINT_RATE";
    public const string DirectBookingRatio = "DIRECT_BOOKING_RATIO";
    public const string ChannelCost = "CHANNEL_COST";
    public const string ConversionRate = "CONVERSION_RATE";

    // ------------------------------------------------------------- Achats et stocks
    public const string InventoryTurnover = "INVENTORY_TURNOVER";
    public const string StockOutRate = "STOCK_OUT_RATE";
    public const string PurchasePriceVariance = "PURCHASE_PRICE_VARIANCE";
    public const string SupplierOnTimeDeliveryRate = "SUPPLIER_ON_TIME_DELIVERY_RATE";
    public const string HousekeepingCostPerRoom = "HOUSEKEEPING_COST_PER_ROOM";
    public const string EnergyCostPerOccupiedRoom = "ENERGY_COST_PER_OCCUPIED_ROOM";
    public const string WaterPerGuestNight = "WATER_PER_GUEST_NIGHT";
}
