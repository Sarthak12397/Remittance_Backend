public class CorridorRate
{
     public Guid Id { get; private set; }
    public Guid CorridorId { get; private set; }
    public string CorridorCode { get; private set; }           
    public RateApprovalStatus ApprovalStatus { get; private set; }


     public decimal BaseRate { get; private set; }              // DECIMAL(18,8) — never float
    public decimal TreasurySpread { get; private set; }
    public decimal PartnerSpread { get; private set; }
    public decimal PromotionalAdjustment { get; private set; } // Zero if no promotion
    public decimal CustomerRate { get; private set; }          // Computed: Base - Treasury - Partner + Promo
    public decimal SettlementRate { get; private set; }        // Rate between company and payout partner
    public int RateLockMinutes { get; private set; } 


       public string SubmittedBy { get; private set; }            // Maker
    public string? ApprovedBy { get; private set; }            // Checker — must differ from SubmittedBy
    public string? ApprovalReason { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
       public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }         // null = still current

    // Audit
    public string RateSource { get; private set; }             // "MANUAL", "IMPORT", "API_PROVIDER"
    public string? SourceIp { get; private set; }
    public DateTime CreatedAt { get; private set; }



        private CorridorRate() { }
 public CorridorRate(
        Guid corridorId,
        string corridorCode,
        decimal baseRate,
        decimal treasurySpread,
        decimal partnerSpread,
        decimal promotionalAdjustment,
        decimal settlementRate,
        int rateLockMinutes,
        DateTime effectiveFrom,
        string submittedBy,
        string rateSource,
        string? sourceIp = null)
    {
        if (baseRate <= 0)
            throw new ArgumentException("Base rate must be positive.");
        if (rateLockMinutes <= 0)
            throw new ArgumentException("Rate lock minutes must be positive.");

        var customerRate = baseRate - treasurySpread - partnerSpread + promotionalAdjustment;
        if (customerRate <= 0)
            throw new NegativeSpreadException(
                $"Computed customer rate {customerRate} is not positive. Check spread configuration.");
        if (settlementRate >= customerRate)
            throw new ArgumentException(
                "Settlement rate must be less than customer rate. FX margin must be positive.");
        if (string.IsNullOrWhiteSpace(submittedBy))
            throw new ArgumentException("SubmittedBy (maker) is required.");

        Id = Guid.NewGuid();
        CorridorId = corridorId;
        CorridorCode = corridorCode;
        BaseRate = baseRate;
        TreasurySpread = treasurySpread;
        PartnerSpread = partnerSpread;
        PromotionalAdjustment = promotionalAdjustment;
        CustomerRate = customerRate;
        SettlementRate = settlementRate;
        RateLockMinutes = rateLockMinutes;
        EffectiveFrom = effectiveFrom;
        SubmittedBy = submittedBy;
        RateSource = rateSource;
        SourceIp = sourceIp;
        ApprovalStatus = RateApprovalStatus.PendingApproval;
        CreatedAt = DateTime.UtcNow;
    }




  public void Approve(string approvedBy, string reason)
    {
        if (ApprovalStatus != RateApprovalStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot approve a rate in status {ApprovalStatus}.");
        if (approvedBy.Equals(SubmittedBy, StringComparison.OrdinalIgnoreCase))
            throw new MakerCheckerViolationException();

        ApprovalStatus = RateApprovalStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovalReason = reason;
        ApprovedAt = DateTime.UtcNow;
    }
      public void Reject(string rejectedBy, string reason)
    {
        if (ApprovalStatus != RateApprovalStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot reject a rate in status {ApprovalStatus}.");

        ApprovalStatus = RateApprovalStatus.Rejected;
        ApprovedBy = rejectedBy;
        ApprovalReason = reason;
        ApprovedAt = DateTime.UtcNow;
    }


    public void Expire(DateTime expiredAt)
    {
        if (ApprovalStatus != RateApprovalStatus.Approved)
            throw new InvalidOperationException($"Only approved rates can be expired. Current: {ApprovalStatus}.");
        EffectiveTo = expiredAt;
        ApprovalStatus = RateApprovalStatus.Expired;
    }

        public bool IsActive(DateTime at) =>
        ApprovalStatus == RateApprovalStatus.Approved &&
        EffectiveFrom <= at &&
        (EffectiveTo == null || EffectiveTo > at);




}