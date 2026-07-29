public class QuoteService
{
    private readonly ICorridorRepository _corridorRepository;
    private readonly ICorridorRateRepository _corridorRateRepository;
    private readonly ISenderRepository _senderRepository;
    private readonly IBeneficiaryRepository _beneficiaryRepository;
    private readonly IFeeRuleRepository _feeRuleRepository;
    private readonly IQuoteRepository _quoteRepository;
    private readonly FeeCalculationService _feeCalculationService;
    private readonly ExtensionService _exchangeRateService;
    private readonly IAuditLogRepository _auditService;

    public QuoteService(
        ICorridorRepository corridorRepository,
        ICorridorRateRepository corridorRateRepository,
        ISenderRepository senderRepository,
        IBeneficiaryRepository beneficiaryRepository,
        IFeeRuleRepository feeRuleRepository,
        IQuoteRepository quoteRepository,
        FeeCalculationService feeCalculationService,
        ExtensionService exchangeRateService,
        IAuditLogRepository auditService)
    {
        _corridorRepository = corridorRepository;
        _corridorRateRepository = corridorRateRepository;
        _senderRepository = senderRepository;
        _beneficiaryRepository = beneficiaryRepository;
        _feeRuleRepository = feeRuleRepository;
        _quoteRepository = quoteRepository;
        _feeCalculationService = feeCalculationService;
        _exchangeRateService = exchangeRateService;
        _auditService = auditService;
    }

    public async Task<QuoteResponse> GenerateQuoteAsync(
        GenerateQuoteRequest request,
        string createdBy)
    {
        var corridor = await _corridorRepository.GetByIdAsync(request.CorridorId)
            ?? throw new CorridorInactiveException(request.CorridorId);

        corridor.ValidateIsActive();
        corridor.ValidatePayoutMethodSupported(request.PayoutMethod);
        corridor.ValidateAmount(request.SendAmount);

        if (corridor.IsCutoffExceeded())
            throw new CutoffTimeExceededException();

        var sender = await _senderRepository.GetByIdAsync(request.SenderId)
            ?? throw new SenderNotFoundException(request.SenderId);

        sender.ValidateKycVerified();

        var beneficiary = await _beneficiaryRepository.GetByIdAsync(request.BeneficiaryId)
            ?? throw new BeneficiaryNotFoundException(request.BeneficiaryId);

        beneficiary.ValidateBelongsToSender(request.SenderId);

        var rate = await _exchangeRateService
            .GetCurrentActiveRateAsync(corridor.Code);

        var feeRule = await _feeRuleRepository.GetByIdAsync(corridor.FeeRuleId)
            ?? throw new FeeRuleNotFoundException(corridor.FeeRuleId);

        decimal serviceFee = _feeCalculationService.CalculateFee(
            feeRule,
            request.SendAmount);

        decimal taxAmount = _feeCalculationService.CalculateTaxAmount(
            feeRule,
            serviceFee);

        decimal payoutAmount = request.SendAmount * rate.CustomerRate;
        decimal totalPayable = request.SendAmount + serviceFee + taxAmount;

        long sequence = await _quoteRepository.GetNextSequenceAsync();

        string quoteReference =
            $"Q-{DateTime.UtcNow:yyyyMMdd}-{sequence:D6}";

        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteReference = quoteReference,
            CorridorId = corridor.Id,
            SenderId = sender.Id,
            BeneficiaryId = beneficiary.Id,
            CustomerRate = rate.CustomerRate,
            SendAmount = request.SendAmount,
            ServiceFee = serviceFee,
            TaxAmount = taxAmount,
            TotalPayable = totalPayable,
            PayoutAmount = payoutAmount,
            RateExpiresAt = rate.ExpiresAt,
            IsConverted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _quoteRepository.AddAsync(quote);
        await _quoteRepository.SaveChangesAsync();

        await _auditService.LogAsync(
            "Quote",
            quote.Id,
            "CREATED",
            createdBy);

        return new QuoteResponse
        {
            QuoteId = quote.Id,
            QuoteReference = quote.QuoteReference,
            CustomerRate = quote.CustomerRate,
            SendAmount = quote.SendAmount,
            ServiceFee = quote.ServiceFee,
            TaxAmount = quote.TaxAmount,
            TotalPayable = quote.TotalPayable,
            PayoutAmount = quote.PayoutAmount,
            RateExpiresAt = quote.RateExpiresAt,
            IsExpired = quote.IsExpired
        };
    }

    public async Task<QuoteResponse> GetQuoteAsync(Guid quoteId)
    {
        var quote = await _quoteRepository.GetByIdAsync(quoteId)
            ?? throw new QuoteNotFoundException(quoteId);

        return new QuoteResponse
        {
            QuoteId = quote.Id,
            QuoteReference = quote.QuoteReference,
            CustomerRate = quote.CustomerRate,
            SendAmount = quote.SendAmount,
            ServiceFee = quote.ServiceFee,
            TaxAmount = quote.TaxAmount,
            TotalPayable = quote.TotalPayable,
            PayoutAmount = quote.PayoutAmount,
            RateExpiresAt = quote.RateExpiresAt,
            IsExpired = quote.IsExpired
        };
    }

    public async Task MarkQuoteAsConvertedAsync(
        Guid quoteId,
        string updatedBy)
    {
        var quote = await _quoteRepository.GetByIdAsync(quoteId)
            ?? throw new QuoteNotFoundException(quoteId);

        if (quote.IsConverted)
            return;

        if (quote.IsExpired)
            throw new QuoteExpiredException();

        quote.IsConverted = true;
        quote.ConvertedAt = DateTime.UtcNow;

        await _quoteRepository.UpdateAsync(quote);
        await _quoteRepository.SaveChangesAsync();

        await _auditService.LogAsync(
            "Quote",
            quote.Id,
            "CONVERTED",
            updatedBy);
    }
}