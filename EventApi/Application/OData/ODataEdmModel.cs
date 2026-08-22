using EventApi.Application.DTOs;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace EventApi.Application.OData;

public static class ODataEdmModel
{
    public static IEdmModel GetEdmModel()
    {
        var builder = new ODataConventionModelBuilder();
        builder.EntitySet<EventODataDto>("Events");
        builder.EntitySet<RegistrationODataDto>("Registrations");
        return builder.GetEdmModel();
    }
}
