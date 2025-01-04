using FluentMigrator;

namespace InboxPriorityQueue.Migrations;

[Migration(202431121500)]
public class Init_202431121500 : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
create table ""InboxItems""
(
    ""Item""     text     not null,
    ""ItemHash"" uuid generated always as (uuid_in((md5(""Item""))::cstring)) stored,
    ""Status""   smallint not null,
    ""Priority"" smallint not null,
    ""Id""       integer generated always as identity (cycle)
        constraint inboxitems_pk
            primary key
);

create index priority_status_index
    on ""InboxItems"" (""Priority"" desc, ""Status"" desc);

create unique index direction_index
    on ""InboxItems"" (""ItemHash"", ""Item"");
");
     
    }

    public override void Down()
    {
        Execute.Sql(@"drop table ""InboxItems""");
    }
}