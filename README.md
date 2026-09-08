# Retail Inventory & Sales Management System
## .NET Desktop Application (WPF / SQLite / MVVM)

A modern, responsive, and robust desktop retail point-of-sale (POS) and inventory management solution.

---

### Quick Start Guide

1. **Prerequisites**: .NET 8 SDK (or .NET 10 SDK) installed on Windows 10/11.
2. **Build the Solution**:
   ```powershell
   dotnet build RetailApp.sln -c Release
   ```
3. **Run the Application**:
   ```powershell
   dotnet run --project RetailApp/RetailApp.csproj -c Release
   ```
   *Or double-click:* `RetailApp\bin\Release\net8.0-windows\RetailApp.exe`
4. **Run Unit Tests**:
   ```powershell
   dotnet test RetailApp.sln
   ```

### Key Highlights
- **Product Management**: Full CRUD, unique SKU validation, category filtering, and safety soft-delete.
- **Sales POS**: Live stock validation, quantity adjusters, discount/tax calculations, cash tendered & change due, atomic SQLite transactions, and receipt printer dialog.
- **Stock Management & Audit**: Restock orders, damaged/expired write-offs with required audit notes, and complete movement ledger.
- **Transaction History**: Date filters, search, line items inspector, reprint receipts, and CSV export.
- **Executive Analytics Dashboard (5th Feature)**: Real-time revenue & stock KPI metrics, top-sellers ranking, low-stock warnings, and CSV export.
