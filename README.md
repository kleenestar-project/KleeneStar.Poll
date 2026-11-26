![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar.Poll - Scheduling Polls and Decisions

**KleeneStar.Poll** adds polls (*Umfragen*) to the **KleeneStar** platform. A poll is an object
like an issue or an appointment - with workflow, comments, attachments, relations, history and
permissions - and the plugin adds its options, the votes and the result.

- **Scheduling polls** (*Zeitabstimmung*): propose dates, everybody answers each with yes, if
  need be or no, and the date most can make is fixed.
- **Decisions** (*Entscheidung*): a question with options, one choice or several.
- **Further types** through the catalog `IPollType`, from any plugin.
- **Deadlines**: polls close automatically when their time is up.
- **Anonymous** polls and polls **hiding their result** until they are closed.
- **Sidebar**: *Polls* below the calendar, with the running polls below the kind links.
- **Status** from the class's workflow, kept in step with closing and reopening.

Built on the **WebExpress-Framework** and **KleeneStar.Core**. The concept is in
[docs/kleenestar.poll.md](docs/kleenestar.poll.md).

## Build

The repository is cloned beside the other KleeneStar repositories; the host references it.

```
dotnet build KleeneStar/src/KleeneStar.slnx
KleeneStar.Poll/src/KleeneStar.Poll.Test/bin/Debug/net11.0/KleeneStar.Poll.Test.exe
```

## License

MIT, see [LICENSE](LICENSE).
