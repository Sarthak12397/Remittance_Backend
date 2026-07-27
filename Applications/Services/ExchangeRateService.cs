public class ExtensionService
{
    private readonly ICorridorRepository _corridorRepository;
    private readonly ICorridorRateRepository _corridorRateRepository;
    private readonly IRateApprovalLogRepository _rateApprovalRepository;

    private readonly IAuditLogRepository _auditService;
    private const decimal VarianceThresholdPercent = 5.0m; // configurable


    public ExtensionService(IAuditLogRepository auditlogRepository, ICorridorRepository corridorRepository, ICorridorRateRepository corridorRateRepository, IRateApprovalLogRepository rateApprovalLogRepository)


    {
        _corridorRepository = corridorRepository;
        _corridorRateRepository = corridorRateRepository;
        _rateApprovalRepository = rateApprovalLogRepository;
        _auditService = auditlogRepository;


    }
public async Task<Guid> SubmitRateSheetAsync(
  string   corridorCode,
        decimal  baseRate,
        decimal  treasurySpread,
        decimal  partnerSpread,
        decimal  promotionalAdjustment,
        decimal  settlementRate,
        int      rateLockMinutes,
        DateTime effectiveFrom,
        string   submittedBy,
        string   sourceIp,
        string   rateSource,
        CancellationToken ct = default)
{
    // Step 1: Fetch corridor
    var corridor = await _corridorRepository.GetByCodeAsync(corridorCode, ct);
        if (corridor == null)
        throw new CorridorInactiveException(corridorCode);

         if (baseRate <= 0)
            throw new ArgumentException("Base rate must be positive.", nameof(baseRate));


              var customerRate = baseRate - treasurySpread - partnerSpread + promotionalAdjustment;
        if (customerRate <= 0)
            throw new NegativeSpreadException(
                $"Computed customer rate {customerRate} is not positive. Check spread configuration.");

        // Step 4: Fetch current active rate. Check variance against threshold.
        var currentActive = await _corridorRateRepository
            .GetCurrentActiveRateAsync(corridor.Id, DateTime.UtcNow, ct);

        if (currentActive != null)
        {
            var variance = Math.Abs(
                (customerRate - currentActive.CustomerRate) / currentActive.CustomerRate) * 100;

            if (variance > VarianceThresholdPercent)
                throw new RateVarianceThresholdException(variance, VarianceThresholdPercent);
        }


           var newRate = new CorridorRate(
            corridor.Id,
            corridorCode,
            baseRate,
            treasurySpread,
            partnerSpread,
            promotionalAdjustment,
            settlementRate,
            rateLockMinutes,
            effectiveFrom,
            submittedBy,
            rateSource,
            sourceIp);



      await _corridorRateRepository.AddAsync(newRate, ct);

             await _rateApprovalRepository.AddAsync(new RateApprovalLog(
            newRate.Id,
            corridorCode,
            "SUBMITTED",
            currentActive?.CustomerRate ?? 0m,
            customerRate,
            reason:      null,
            performedBy: submittedBy,
            ipAddress:   sourceIp), ct);
    await _auditService.AddAsync(new AuditLog(
            entityType:   "CorridorRate",
            entityId:     newRate.Id,
            action:       "SUBMITTED",
            beforeValue:  null,
            afterValue:   null,
            performedBy:  submittedBy,
            ipAddress:    sourceIp,
            deviceInfo:   null,
            correlationId: null), ct);

        return newRate.Id;
}
  public async Task ApproveRateSheetAsync(
        Guid   rateId,
        string approvedBy,
        string reason,
        CancellationToken ct = default)
    {
        var rate = await _corridorRateRepository.GetByIdAsync(rateId, ct) ?? throw new InvalidOperationException($"Rate {rateId} not found");
        
        if(rate.ApprovalStatus != RateApprovalStatus.PendingApproval)
        {
                       throw new InvalidOperationException(
                $"Cannot approve rate in status {rate.ApprovalStatus}. Expected: PendingApproval.");
        }

    }
    
}










}