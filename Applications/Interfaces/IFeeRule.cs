public interface IFeeRule
{
    decimal CalculateFee(decimal sendAmount);
}