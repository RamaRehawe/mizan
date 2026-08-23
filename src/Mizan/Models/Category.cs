namespace Mizan.Models;

public class Category
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public required string Name { get; set; }
    public CategoryKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
