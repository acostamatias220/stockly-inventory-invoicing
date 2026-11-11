using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stockly.Application.Common;
using Stockly.Domain.Entities;
using Stockly.Domain.Enums;

namespace Stockly.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Load the demo dataset on startup when the database is empty.</summary>
    public bool Enabled { get; set; }

    /// <summary>Password for the three demo users. Empty → a random one is generated and logged once.</summary>
    public string? DemoPassword { get; set; }
}

/// <summary>
/// Deterministic demo dataset (Random seed 42): an IT/office supplies distributor with three warehouses,
/// ~90 days of stock movements and invoices in every state. Stock levels are derived from the movements,
/// so the ledger and the balances always match.
/// </summary>
public sealed class DbSeeder(AppDbContext db, IPasswordHasher hasher, IBusinessClock clock, ILogger<DbSeeder> logger)
{
    public static readonly string[] DemoEmails = ["admin@stockly.dev", "ventas@stockly.dev", "deposito@stockly.dev"];

    private readonly Random _random = new(42);
    private readonly Dictionary<(int Product, int Warehouse), int> _stock = [];
    private readonly Dictionary<InvoiceLetter, long> _numbers = new() { [InvoiceLetter.A] = 0, [InvoiceLetter.B] = 0 };

    public async Task SeedAsync(string? demoPassword, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        var password = string.IsNullOrWhiteSpace(demoPassword) ? GeneratePassword() : demoPassword;
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogWarning("Seed: no Seed:DemoPassword configured. Generated password for demo users: {Password}", password);
        }

        var now = clock.UtcNow;
        var start = now.Date.AddDays(-95);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var users = new[]
        {
            User.Create(DemoEmails[0], "Laura Gómez", UserRole.Admin, start),
            User.Create(DemoEmails[1], "Diego Fernández", UserRole.Vendedor, start),
            User.Create(DemoEmails[2], "Sofía Martínez", UserRole.Deposito, start),
        };
        foreach (var user in users)
        {
            user.SetPasswordHash(hasher.Hash(user, password), start);
        }

        db.Users.AddRange(users);

        var categories = new Dictionary<string, Category>
        {
            ["comp"] = Category.Create("Computación", "Notebooks, PCs y componentes", "#6366F1"),
            ["peri"] = Category.Create("Periféricos", "Monitores, teclados, mouse y audio", "#0EA5E9"),
            ["redes"] = Category.Create("Redes", "Routers, switches y cableado", "#10B981"),
            ["alm"] = Category.Create("Almacenamiento", "Discos, SSD y pendrives", "#F59E0B"),
            ["ofi"] = Category.Create("Oficina", "Papelería e insumos de impresión", "#EC4899"),
            ["elec"] = Category.Create("Electricidad", "UPS, zapatillas y cargadores", "#8B5CF6"),
        };
        db.Categories.AddRange(categories.Values);

        var warehouses = new[]
        {
            Warehouse.Create("CEN", "Depósito Central", "Av. Caseros 3100, Parque Patricios, CABA"),
            Warehouse.Create("NOR", "Sucursal Norte", "Av. Maipú 1800, Vicente López, Bs. As."),
            Warehouse.Create("CBA", "Depósito Córdoba", "Bv. San Juan 900, Córdoba"),
        };
        db.Warehouses.AddRange(warehouses);

        var suppliers = new[]
        {
            Supplier.Create("Tecnodistribuidora Mayorista S.A.", Cuit("30", "70988123"), "Martín Ruiz", "compras@tecnomayorista.example", "11 4555-1200", "Av. Belgrano 2450, CABA", start),
            Supplier.Create("NetSur Importaciones S.R.L.", Cuit("30", "71455320"), "Carla Benítez", "ventas@netsur.example", "11 4789-3300", "Parque Industrial Pilar, Bs. As.", start),
            Supplier.Create("Papelera del Plata S.A.", Cuit("30", "50012345"), "Jorge Ibarra", "pedidos@papeleradelplata.example", "11 4301-7788", "Av. Montes de Oca 1500, CABA", start),
            Supplier.Create("Energía Total S.A.", Cuit("30", "69874521"), "Paula Sosa", "comercial@energiatotal.example", "351 422-8800", "Av. Colón 4100, Córdoba", start),
        };
        db.Suppliers.AddRange(suppliers);

        var customers = new[]
        {
            Customer.Create("Estudio Contable Ferreyra & Asoc.", Cuit("30", "71590234"), TaxCondition.ResponsableInscripto, "administracion@ferreyra.example", "11 4322-1010", "Lavalle 1450, CABA", start),
            Customer.Create("Colegio San Martín de Tours", Cuit("30", "52876410"), TaxCondition.Exento, "compras@sanmartin.example", "11 4801-2233", "Av. del Libertador 3800, CABA", start),
            Customer.Create("Agroservicios Pampa S.A.", Cuit("30", "70223311"), TaxCondition.ResponsableInscripto, "it@agropampa.example", "2477 43-1200", "Ruta 8 km 225, Pergamino", start),
            Customer.Create("Clínica Odontológica Sonrisas", Cuit("30", "71102938"), TaxCondition.ResponsableInscripto, "admin@sonrisas.example", "11 4961-5577", "Av. Santa Fe 2650, CABA", start),
            Customer.Create("Lucía Paredes (Diseño gráfico)", Cuit("27", "34567812"), TaxCondition.Monotributista, "lucia.paredes@mail.example", "11 5566-7788", "Gorriti 4800, CABA", start),
            Customer.Create("Martín Quiroga", "33456789", TaxCondition.ConsumidorFinal, "mquiroga@mail.example", "11 6012-3456", "Juramento 2100, CABA", start),
            Customer.Create("Logística Andina S.R.L.", Cuit("30", "71678345"), TaxCondition.ResponsableInscripto, "sistemas@logandina.example", "261 420-9900", "Acceso Este 1500, Mendoza", start),
            Customer.Create("Café Notable SAS", Cuit("30", "71789456"), TaxCondition.ResponsableInscripto, "hola@cafenotable.example", "11 4342-6060", "Av. de Mayo 825, CABA", start),
            Customer.Create("Hernán Villalba (Electricista)", Cuit("20", "28765432"), TaxCondition.Monotributista, "hvillalba@mail.example", "351 655-4433", "Barrio General Paz, Córdoba", start),
            Customer.Create("Municipalidad de Tigre - Sec. Modernización", Cuit("30", "99912345"), TaxCondition.Exento, "modernizacion@tigre.example", "11 4512-4400", "Av. Cazón 1514, Tigre", start),
            Customer.Create("Valentina Rossi", "40123987", TaxCondition.ConsumidorFinal, null, "11 3344-5566", null, start),
            Customer.Create("Software Factory Delta S.A.", Cuit("30", "71890567"), TaxCondition.ResponsableInscripto, "compras@sfdelta.example", "11 5263-1000", "Av. Corrientes 800, CABA", start),
        };
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(ct);

        var catalog = new (string Sku, string Name, string Cat, int Sup, decimal Price, decimal Cost, VatRate Vat, int Min)[]
        {
            ("NB-LEN-14", "Notebook Lenovo IdeaPad 14\" Ryzen 5 16GB 512GB", "comp", 0, 1_150_000m, 910_000m, VatRate.Reducido, 4),
            ("NB-HP-15", "Notebook HP 15\" Core i5 8GB 512GB", "comp", 0, 980_000m, 770_000m, VatRate.Reducido, 4),
            ("NB-DELL-LAT", "Notebook Dell Latitude 3450 Core i7 16GB", "comp", 0, 1_690_000m, 1_350_000m, VatRate.Reducido, 2),
            ("PC-OFI-I3", "PC de escritorio oficina Core i3 8GB SSD 240GB", "comp", 0, 640_000m, 495_000m, VatRate.Reducido, 3),
            ("RAM-DDR4-16", "Memoria RAM DDR4 16GB 3200MHz", "comp", 0, 62_000m, 44_000m, VatRate.Reducido, 10),
            ("MON-24-SAM", "Monitor Samsung 24\" FHD IPS", "peri", 0, 245_000m, 182_000m, VatRate.Reducido, 6),
            ("MON-27-LG", "Monitor LG 27\" QHD 75Hz", "peri", 0, 389_000m, 298_000m, VatRate.Reducido, 3),
            ("TEC-LOG-K120", "Teclado Logitech K120 USB", "peri", 0, 18_500m, 11_900m, VatRate.General, 20),
            ("MOU-LOG-M170", "Mouse inalámbrico Logitech M170", "peri", 0, 15_900m, 9_800m, VatRate.General, 25),
            ("COMBO-LOG-MK270", "Combo teclado + mouse Logitech MK270", "peri", 0, 39_900m, 27_500m, VatRate.General, 12),
            ("AUR-HX-CLOUD", "Auriculares HyperX Cloud Stinger", "peri", 0, 72_000m, 51_000m, VatRate.General, 6),
            ("CAM-LOG-C920", "Webcam Logitech C920 Full HD", "peri", 0, 118_000m, 86_000m, VatRate.General, 5),
            ("RT-TPL-AX23", "Router TP-Link Archer AX23 WiFi 6", "redes", 1, 98_000m, 69_000m, VatRate.General, 6),
            ("SW-TPL-8P", "Switch TP-Link 8 puertos Gigabit", "redes", 1, 41_500m, 28_900m, VatRate.General, 8),
            ("AP-UBI-U6", "Access Point Ubiquiti UniFi U6 Lite", "redes", 1, 189_000m, 142_000m, VatRate.General, 3),
            ("CAB-UTP-305", "Bobina cable UTP Cat6 305 m", "redes", 1, 265_000m, 198_000m, VatRate.General, 2),
            ("PATCH-1M", "Patch cord Cat6 1 m", "redes", 1, 3_200m, 1_700m, VatRate.General, 50),
            ("SSD-KIN-480", "SSD Kingston A400 480GB", "alm", 0, 48_500m, 34_000m, VatRate.Reducido, 15),
            ("SSD-NVME-1T", "SSD NVMe WD Blue SN580 1TB", "alm", 0, 112_000m, 83_000m, VatRate.Reducido, 8),
            ("HDD-EXT-2T", "Disco externo Seagate 2TB USB 3.0", "alm", 0, 135_000m, 101_000m, VatRate.Reducido, 5),
            ("PEN-SAN-64", "Pendrive SanDisk 64GB", "alm", 0, 12_900m, 7_600m, VatRate.General, 30),
            ("RESMA-A4", "Resma papel A4 75g x500", "ofi", 2, 8_900m, 6_100m, VatRate.General, 80),
            ("TONER-HP-85A", "Tóner HP 85A compatible", "ofi", 2, 24_500m, 15_200m, VatRate.General, 15),
            ("CART-EPS-544", "Botella tinta Epson T544 (negro)", "ofi", 2, 11_200m, 7_300m, VatRate.General, 20),
            ("CUAD-A4-ESP", "Cuaderno espiral A4 rayado x84", "ofi", 2, 4_300m, 2_600m, VatRate.General, 40),
            ("UPS-LYO-1200", "UPS Lyonn 1200VA", "elec", 3, 158_000m, 118_000m, VatRate.General, 4),
            ("ZAP-6T-SUP", "Zapatilla 6 tomas con protección", "elec", 3, 19_800m, 12_400m, VatRate.General, 15),
            ("CARG-USBC-65", "Cargador USB-C 65W universal", "elec", 3, 38_000m, 25_000m, VatRate.General, 10),
        };

        var products = catalog.Select(p => Product.Create(
            p.Sku, p.Name, null, categories[p.Cat].Id, suppliers[p.Sup].Id, p.Price, p.Cost, p.Vat, p.Min, start)).ToList();
        db.Products.AddRange(products);
        await db.SaveChangesAsync(ct);

        var admin = users[0];
        var deposito = users[2];
        var vendedor = users[1];

        // 1) Opening stock: purchases received ~90 days ago (main warehouse gets most of it).
        foreach (var product in products)
        {
            var minimum = product.MinimumStock;
            Move(product, warehouses[0], _random.Next(minimum * 2, minimum * 6 + 10), StockMovementType.Entrada, deposito.Id, start.AddHours(10), "Stock inicial", "Remito R-0001", product.SupplierId);
            Move(product, warehouses[1], _random.Next(minimum, minimum * 3 + 4), StockMovementType.Entrada, deposito.Id, start.AddHours(11), "Stock inicial", "Remito R-0001", product.SupplierId);
            if (_random.NextDouble() < 0.6)
            {
                Move(product, warehouses[2], _random.Next(1, minimum * 2 + 3), StockMovementType.Entrada, deposito.Id, start.AddHours(12), "Stock inicial", "Remito R-0002", product.SupplierId);
            }
        }

        // 2) ~90 days of operations.
        for (var day = 4; day <= 95; day++)
        {
            var date = start.AddDays(day);
            if (date > now)
            {
                break;
            }

            if (date.DayOfWeek is DayOfWeek.Sunday)
            {
                continue;
            }

            var invoicesToday = _random.Next(0, date.DayOfWeek == DayOfWeek.Saturday ? 2 : 4);
            for (var n = 0; n < invoicesToday; n++)
            {
                await CreateInvoiceAsync(customers, products, warehouses[_random.NextDouble() < 0.75 ? 0 : 1], _random.NextDouble() < 0.7 ? vendedor : admin, date.AddHours(9 + _random.Next(0, 9)), now, ct);
            }

            if (day % 14 == 0)
            {
                // Replenishment from suppliers.
                foreach (var product in products.Where(p => Balance(p, warehouses[0]) < p.MinimumStock * 2).Take(8))
                {
                    Move(product, warehouses[0], product.MinimumStock * 4, StockMovementType.Entrada, deposito.Id, date.AddHours(8), "Reposición", $"Remito R-{day:D4}", product.SupplierId);
                }
            }

            if (day % 9 == 0)
            {
                var product = products[_random.Next(products.Count)];
                var qty = Math.Min(Balance(product, warehouses[0]), _random.Next(2, 6));
                if (qty > 0)
                {
                    var correlation = Guid.NewGuid();
                    Move(product, warehouses[0], -qty, StockMovementType.Transferencia, deposito.Id, date.AddHours(15), "Reabastecer sucursal", "CEN → NOR", null, correlation);
                    Move(product, warehouses[1], qty, StockMovementType.Transferencia, deposito.Id, date.AddHours(15), "Reabastecer sucursal", "CEN → NOR", null, correlation);
                }
            }

            if (day % 23 == 0)
            {
                var product = products[_random.Next(products.Count)];
                if (Balance(product, warehouses[0]) > 1)
                {
                    Move(product, warehouses[0], -1, StockMovementType.Ajuste, deposito.Id, date.AddHours(18), "Conteo físico: unidad dañada", null, null);
                }
            }
        }

        // 3) A couple of products deliberately under their minimum so the alerts have something to show.
        foreach (var sku in new[] { "AP-UBI-U6", "CAM-LOG-C920", "TONER-HP-85A" })
        {
            var product = products.Single(p => p.Sku == sku);
            foreach (var warehouse in warehouses)
            {
                var balance = Balance(product, warehouse);
                if (balance > 1)
                {
                    Move(product, warehouse, -(balance - 1), StockMovementType.Salida, deposito.Id, now.AddHours(-30), "Entrega para uso interno / muestras", null, null);
                }
            }
        }

        db.StockLevels.AddRange(_stock.Select(s => new StockLevel(s.Key.Product, s.Key.Warehouse, s.Value)));
        db.Set<InvoiceSequence>().AddRange(_numbers.Select(n => new InvoiceSequence(n.Key, 1, n.Value)));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Seed: demo data loaded ({Products} products, {Invoices} invoices).", products.Count, _numbers.Values.Sum());
    }

    private async Task CreateInvoiceAsync(
        Customer[] customers, List<Product> products, Warehouse warehouse, User seller, DateTime at, DateTime now, CancellationToken ct)
    {
        var customer = customers[_random.Next(customers.Length)];
        var age = (now - at).TotalDays;
        var invoice = Invoice.CreateDraft(customer, warehouse.Id, 1, DateOnly.FromDateTime(at.AddDays(15)),
            _random.NextDouble() < 0.15 ? 5 : 0, null, seller.Id, at);

        var lines = new List<InvoiceLine>();
        foreach (var product in products.OrderBy(_ => _random.Next()).Take(_random.Next(1, 5)))
        {
            var available = Balance(product, warehouse) - lines.Where(l => l.ProductId == product.Id).Sum(l => l.Quantity);
            var quantity = Math.Min(available, product.Price > 300_000 ? _random.Next(1, 3) : _random.Next(1, 8));
            if (quantity <= 0)
            {
                continue;
            }

            var discount = _random.NextDouble() < 0.2 ? 10 : 0;
            lines.Add(InvoiceLine.Create(product.Id, product.Sku, product.Name, quantity, product.Price, discount, product.VatRate));
        }

        if (lines.Count == 0)
        {
            return;
        }

        invoice.ReplaceLines(lines, at);
        db.Invoices.Add(invoice);

        // Recent ones stay as drafts sometimes; everything else is issued.
        if (age < 2 && _random.NextDouble() < 0.5)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var number = ++_numbers[invoice.Letter];
        invoice.Issue(number, DateOnly.FromDateTime(at), at);
        await db.SaveChangesAsync(ct);

        var correlation = Guid.NewGuid();
        foreach (var group in lines.GroupBy(l => l.ProductId))
        {
            var product = products.Single(p => p.Id == group.Key);
            Move(product, warehouse, -group.Sum(l => l.Quantity), StockMovementType.Venta, seller.Id, at, "Venta", invoice.FormattedNumber, null, correlation, invoice.Id);
        }

        var roll = _random.NextDouble();
        if (roll < 0.04)
        {
            var cancelAt = at.AddHours(3);
            invoice.Cancel("Error en los datos del cliente", cancelAt);
            var restock = Guid.NewGuid();
            foreach (var group in lines.GroupBy(l => l.ProductId))
            {
                var product = products.Single(p => p.Id == group.Key);
                Move(product, warehouse, group.Sum(l => l.Quantity), StockMovementType.AnulacionVenta, seller.Id, cancelAt, invoice.CancellationReason, invoice.FormattedNumber, null, restock, invoice.Id);
            }
        }
        else if (age > 20 ? roll < 0.92 : age > 5 && roll < 0.55)
        {
            var methods = Enum.GetValues<PaymentMethod>();
            invoice.MarkAsPaid(methods[_random.Next(methods.Length)], at.AddDays(_random.Next(0, Math.Min(14, (int)age))));
        }

        await db.SaveChangesAsync(ct);
    }

    private void Move(
        Product product, Warehouse warehouse, int delta, StockMovementType type, int userId, DateTime at,
        string? reason, string? reference, int? supplierId, Guid? correlation = null, int? invoiceId = null)
    {
        var key = (product.Id, warehouse.Id);
        var balance = _stock.GetValueOrDefault(key) + delta;
        _stock[key] = balance;
        db.StockMovements.Add(StockMovement.Record(
            product.Id, warehouse.Id, type, delta, balance, userId, correlation ?? Guid.NewGuid(), at,
            reason, reference, invoiceId, type == StockMovementType.Entrada ? supplierId : null));
    }

    private int Balance(Product product, Warehouse warehouse) => _stock.GetValueOrDefault((product.Id, warehouse.Id));

    /// <summary>Builds a CUIT with a valid check digit from its prefix and body.</summary>
    private static string Cuit(string prefix, string body)
    {
        var digits = prefix + body;
        int[] weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var sum = weights.Select((w, i) => w * (digits[i] - '0')).Sum();
        var check = 11 - (sum % 11);
        check = check switch { 11 => 0, 10 => 9, _ => check };
        return digits + check;
    }

    private static string GeneratePassword() =>
        "Demo-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant() + "7";
}
