public class GenerateQuoteRequest
{
    public Guid CorridorId { get; set; }

    public Guid SenderId { get; set; }

    public Guid BeneficiaryId { get; set; }

    public decimal SendAmount { get; set; }

    public string PayoutMethod { get; set; }
}