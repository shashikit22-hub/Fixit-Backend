using ClosedXML.Excel;
using backend.Models;

namespace backend.Services;

public class ExcelExportService
{
    public byte[] ExportServiceRequests(List<ServiceRequest> requests)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Service Requests");

        // Headers
        var headers = new[] { "ID", "Customer Name", "Phone", "Service Type", "Description", "Status", "Created At", "Updated At", "Assigned Technician" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightCyan;
        }

        // Data rows
        for (int row = 0; row < requests.Count; row++)
        {
            var req = requests[row];
            worksheet.Cell(row + 2, 1).Value = req.Id;
            worksheet.Cell(row + 2, 2).Value = req.CustomerName;
            worksheet.Cell(row + 2, 3).Value = req.CustomerPhone;
            worksheet.Cell(row + 2, 4).Value = req.ServiceType;
            worksheet.Cell(row + 2, 5).Value = req.Description ?? "";
            worksheet.Cell(row + 2, 6).Value = req.Status;
            worksheet.Cell(row + 2, 7).Value = req.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            worksheet.Cell(row + 2, 8).Value = req.UpdatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";

            var techNames = req.Assignments
                .Select(a => a.Technician?.Name ?? "")
                .Where(n => !string.IsNullOrEmpty(n));
            worksheet.Cell(row + 2, 9).Value = string.Join(", ", techNames);
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
