using System.Data;
using Dapper;

namespace HSBGTrackerWebApp.Api.Data;

public sealed class GuidAsStringHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.Value = value.ToString();
        // optional: parameter.DbType = DbType.String;
    }

    public override Guid Parse(object value) => value switch
    {
        Guid g => g,
        string s => Guid.Parse(s),
        _ => Guid.Parse(Convert.ToString(value)!)
    };
}