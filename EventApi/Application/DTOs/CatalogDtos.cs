namespace EventApi.Application.DTOs;

public class LocationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateLocationDto
{
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string? Description { get; set; }
    public int? Capacity { get; set; }
}

public class UpdateLocationDto
{
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; }
}

public class OrganizerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string ContactEmail { get; set; } = null!;
    public string ContactPhone { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateOrganizerDto
{
    public string Name { get; set; } = null!;
    public string ContactEmail { get; set; } = null!;
    public string ContactPhone { get; set; } = null!;
    public string? Description { get; set; }
}

public class UpdateOrganizerDto
{
    public string Name { get; set; } = null!;
    public string ContactEmail { get; set; } = null!;
    public string ContactPhone { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
