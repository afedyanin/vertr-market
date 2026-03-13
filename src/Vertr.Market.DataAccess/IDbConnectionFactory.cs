using System.Data;

namespace Vertr.Market.DataAccess;

public interface IDbConnectionFactory
{
    IDbConnection GetConnection();
}
