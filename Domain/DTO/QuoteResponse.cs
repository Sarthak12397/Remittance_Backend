public class QuoteResponse
{
    public Guid QuoteId { get; set; }
    public string QuoteReference { get; set; }

    public decimal CustomerRate { get; set; }

    public decimal SendAmount { get; set; }

    public decimal ServiceFee { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalPayable { get; set; }

    public decimal PayoutAmount { get; set; }

    public DateTime RateExpiresAt { get; set; }

    public bool IsExpired { get; set; }
}