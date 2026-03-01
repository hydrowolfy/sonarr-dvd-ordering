using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(228)]
    public class add_episode_ordering_to_series : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Series").AddColumn("EpisodeOrdering").AsInt32().WithDefaultValue(0);
        }
    }
}
