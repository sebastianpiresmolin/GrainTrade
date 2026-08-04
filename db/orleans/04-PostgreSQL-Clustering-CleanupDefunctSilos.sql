-- Orleans 10.2.2's AdoNet clustering requires a CleanupDefunctSiloEntriesKey
-- query, but none of the PostgreSQL/SQLServer/MySQL scripts published at the
-- v10.2.2 tag define it, so the silo refuses to start with:
--   "Not all required queries found. Missing are: CleanupDefunctSiloEntriesKey"
--
-- Semantics per RelationalOrleansQueries.CleanupDefunctSiloEntriesAsync:
-- delete membership rows for Dead silos (SiloStatus.Dead = 6, verified against
-- the 10.2.2 assembly) whose IAmAliveTime predates the given date, scoped to
-- one deployment.
--
-- Drop this file once an upstream script ships the query.

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'CleanupDefunctSiloEntriesKey','
    DELETE FROM OrleansMembershipTable
    WHERE DeploymentId = @DeploymentId
        AND @DeploymentId IS NOT NULL
        AND IAmAliveTime < @IAmAliveTime
        AND Status = 6;
')
ON CONFLICT (QueryKey) DO UPDATE SET QueryText = EXCLUDED.QueryText;
