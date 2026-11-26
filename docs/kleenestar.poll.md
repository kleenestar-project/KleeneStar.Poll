![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Polls

**KleeneStar.Poll** (plugin id `kleenestar.poll`) adds the object kind **poll** (`poll`, German
*Umfrage*): scheduling polls (*Zeitabstimmung*, which date suits everybody) and decisions
(*Entscheidung*, which option is taken).

A poll is an ordinary object of a class of the kind: its question (the summary), description,
workflow, comments, attachments, relations, history, security level and permissions are the
object's, and the core's object detail experience shows them. The plugin adds the options, the
votes and how the poll ends.

## Data model

The tables live in `KleeneStar.Model` (part of the migration `InitialCreate`), because the installation has one
database and one migration history; without the plugin they are not read.

| Entity | Content |
|--------|---------|
| `Poll` | One per poll object (`ObjectId` unique, cascade): `Type` (key of the type catalog), `MultipleChoice`, `AllowMaybe`, `Anonymous`, `HideResults`, `Deadline` (UTC), `Closed`, `ClosedById` (`null` for the system), `ChosenOptionId` (what the poll settled on, no FK), `TimeZone` |
| `PollOption` | `ObjectId` (cascade), `Order`, `Text` (the choice, or the remark of a date), `Start`/`End` (UTC, dates only), `AllDay` |
| `PollVote` | `OptionId` (FK to the option, cascade), `ObjectId` (no FK: a second cascading path into `Object` is refused by SQL Server), `IdentityId` (no FK), `Answer` (`No` 0, `Maybe` 1, `Yes` 2); unique per option and account |

A voter has at most one answer per option. In a scheduling poll the voter answers the dates with
yes, if need be or no (an unanswered date does not count); in a decision the chosen options are
answered yes and the others not at all. An anonymous poll still stores who voted, so a voter can
change the vote; it only never shows it.

## Poll types

The types are a catalog (`WebManager/PollTypeManager`, WebExpress's `PluginTypeCatalog`): every
**public sealed** `WebPoll/IPollType` with a public parameterless constructor of any plugin.
There is no enum; `Poll.Type` is the key. A type is described by two traits:

| Type | Key | `Dated` | `Graded` |
|------|-----|---------|----------|
| `SchedulePollType` (*Zeitabstimmung*) | `schedule` | yes: options are dates | yes: yes / if need be / no per option |
| `DecisionPollType` (*Entscheidung*) | `decision` | no: options are texts | no: one choice, or several with `MultipleChoice` |

An unknown key (a type whose plugin went away) is read as a decision. Changing the type of a poll
removes every vote.

## Result

`WebPoll/PollResult.Tally` counts only votes naming an option of the poll and an answer the poll
allows. A decision is won by the option chosen most often; a scheduling poll by the date most
voters can make (yes or if need be), among those by the most plain yes. Ties lead together; an
option nobody answered positively leads nothing. The percentage of an option is the share of the
voters answering yes or if need be.

## Lifecycle

- **Create**: the dialog of the polls page, or the core's object wizard in a poll class; a poll
  created without settings (wizard, automation, clone) gets a decision without options, which its
  page offers to complete.
- **Vote**: while the poll runs (not closed, deadline not passed).
- **Close** by hand (the dialog proposes the leading option; *none* fixes nothing), by the deadline
  (`PollCloser` every `AutoCloseInterval` minutes, without actor, idempotent on several replicas)
  or by moving the workflow into a closing state. Closing moves the workflow into the first
  closing state it leads to (the template: *Decided* before *Cancelled*); a close without chosen
  option settles on the single leader, if any.
- **Reopen** by hand or by moving the workflow out of its closing state: the result is forgotten,
  the votes stay, a deadline that has passed is dropped.

`PollManager` keeps the poll's `Closed` mark and its workflow in step through
`WorkflowManager.TransitionExecuted`; its own transitions are not answered again.

## Options in the dialog

One option per line (`WebPoll/PollOptionText`). A date is written in the reader's zone and
culture or as ISO: `25.11.2026 14:00 - 15:30`, `25.11.2026 14:00` (lasting `DefaultDuration`),
`25.11.2026` or `25.11.2026 - 27.11.2026` (whole days), `25.11.2026 22:00 - 26.11.2026 02:00`, and
optionally a remark after `|`. At most 50 options; a decision needs two, a scheduling poll one. On
saving, an option equal to a stored one (same date and text, ignoring case) keeps its id and its
votes; a changed or removed option loses them, and a removed chosen option is no longer chosen.

## UI

- **Sidebar**: *Umfragen* below *Kalender* (order 3) and above *Issues* (order 5) on every kind
  page where the workspace has a poll class (`PollSidebarLinkFragment`, order 4). On the polls and
  poll pages, below the kind links: *Running polls* - the polls still taking votes, the next
  deadline first, at most `RunningCount`, a check mark where the reader voted, the deadline as
  badge (amber within a day), polls whose workflow is closed left out.
- **Polls page** (`/polls/{workspacekey}`, an `IScopeObjectKindOverview`): *Running* and *Closed*
  (the last 50) with type, turnout, deadline or result; headline *New poll*.
- **Poll page** (`/poll/{objectkey}`, an `IScopeObjectKindDetail`): the core's detail experience
  plus the result card (state, result, per option a bar, the counts and - unless anonymous - the
  names; *Vote* / *Change vote*, *Edit*, *Close* / *Reopen*) and the settings card. A poll hiding
  its results shows the turnout only, except to whoever may change it.
- **Dialogs**: *New poll* / *Edit poll* (tabs *Poll*: type, class, question, options, description;
  *Settings*: deadline, if need be, multiple choice, anonymous, hide result), *Vote* (a segmented
  yes / if need be / no per date, radio buttons or check boxes for a decision), *Close poll*,
  *Reopen poll*.

## REST endpoints

Plugin routes lie under `/kleenestar/kleenestar.poll/`.

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `api/1/polls/{workspacekey}` | GET, POST | Create dialog (`?type=` or `?id=` preselects the type) |
| `api/1/polls/{workspacekey}/classes` | GET | Poll classes the caller may create in |
| `api/1/poll/{objectkey}` | GET, PUT, PATCH, POST | Edit dialog |
| `api/1/poll/{objectkey}/vote` | GET, POST | Vote of the caller (`Answer_{option}`, `Choice`, `Choice_{option}`; option ids in `N` format); no answer withdraws |
| `api/1/poll/{objectkey}/close` | GET, POST | Close (`Chosen`: option id or `none`) |
| `api/1/poll/{objectkey}/reopen` | GET, POST | Reopen |

## Permissions

The plugin's pages guard themselves (`WebPoll/PollAuthorization`): the polls page needs
`WorkspaceReadContentPermission`; creating needs the right to create in a poll class; changing,
closing and reopening `ContentAuthorization.MayWrite`. Voting is taking part like commenting: a
signed-in account with `ObjectCommentPermission` on the poll; nobody (`Guid.Empty`) never votes.
Polls are read through `ObjectManager`, so content visibility and security levels apply; the
closing at the deadline runs unrestricted.

## Audit

`PollManager.ConnectAudit` records changed settings, ballots (`PollBallot`: who voted in which poll
and how many answers, never which), closing and reopening.

## Settings

`Plugins:kleenestar.poll`: `TimeZone` (zone of dates when the account names none; default the
host's), `AutoCloseInterval` (5, 0 switches the closing off), `RunningCount` (10),
`DefaultDuration` (60 minutes for a date given by its start only, 0 for a point in time).

## Workspace template

*Team polls* (`kleenestar.poll.teampolls`, suggested key `VOTE`): one class `Poll` with the
workflow *Open* → *Decided* / *Cancelled* and back.
