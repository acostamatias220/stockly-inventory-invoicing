using Stockly.Domain.Enums;

namespace Stockly.Domain.Entities;

/// <summary>
/// Last number used per (letter, point of sale). Incremented with an atomic UPDATE inside the
/// same transaction that issues the invoice, so numbers are correlative and never duplicated.
/// </summary>
public class InvoiceSequence
{
    private InvoiceSequence() { }

    public InvoiceSequence(InvoiceLetter letter, int pointOfSale, long lastNumber = 0)
    {
        Letter = letter;
        PointOfSale = pointOfSale;
        LastNumber = lastNumber;
    }

    public InvoiceLetter Letter { get; private set; }
    public int PointOfSale { get; private set; }
    public long LastNumber { get; private set; }
}
