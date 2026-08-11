using System.ComponentModel.DataAnnotations;
using UniSecretApi.Enums;

namespace UniSecretApi.Entities;

public class Report
{
    public int Id { get; set; }

    public int ReporterId { get; set; } // FK -> Always hidden from public
    public User Reporter { get; set; } = null!;

    public int ReportableId { get; set; } // Confession or Comment ID

    [Required]
    [MaxLength(20)]
    public string ReportableType { get; set; } = string.Empty; // "Confession" or "Comment"

    public ReportReason Reason { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}