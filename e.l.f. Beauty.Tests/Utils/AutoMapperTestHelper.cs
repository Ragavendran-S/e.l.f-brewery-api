using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;

public static class AutoMapperTestHelper
{
    public static MapperConfiguration CreateConfiguration(Action<IMapperConfigurationExpression> configure)
    {
        var configurationExpression = new MapperConfigurationExpression();
        configure(configurationExpression);
        return new MapperConfiguration(configurationExpression, NullLoggerFactory.Instance);
    }
}
