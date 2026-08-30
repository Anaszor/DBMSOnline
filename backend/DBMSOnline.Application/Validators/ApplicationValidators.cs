using FluentValidation;
using DBMSOnline.Application.DTOs;

namespace DBMSOnline.Application.Validators;

public class ConnectionRequestDtoValidator : AbstractValidator<ConnectionRequestDto>
{
    public ConnectionRequestDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Host).NotEmpty().Unless(x => x.DatabaseType == Domain.Enums.DatabaseType.Sqlite);
        RuleFor(x => x.Port).GreaterThan(0).Unless(x => x.DatabaseType == Domain.Enums.DatabaseType.Sqlite || !string.IsNullOrEmpty(x.CustomConnectionString));
    }
}

public class QueryRequestDtoValidator : AbstractValidator<QueryRequestDto>
{
    public QueryRequestDtoValidator()
    {
        RuleFor(x => x.ConnectionId).NotEmpty();
        RuleFor(x => x.QueryText).NotEmpty().MaximumLength(100000);
        RuleFor(x => x.MaxRows).InclusiveBetween(1, 50000);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(1, 300);
    }
}

public class DataPageRequestDtoValidator : AbstractValidator<DataPageRequestDto>
{
    public DataPageRequestDtoValidator()
    {
        RuleFor(x => x.ConnectionId).NotEmpty();
        RuleFor(x => x.TableName).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 1000);
    }
}
