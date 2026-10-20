# Lessons

Captured lessons (per `CLAUDE.md` §0.7). Review for promotion to `CLAUDE.md` §24 when validated.

Started on 2026-10-20 for the nopCommerce 5.00 repository. Entries #1–#9 are carried over from the 4.9 repository
(`C:\Work\TIG_Nop_4_9\tasks\lessons.md`; its number is given as "4.9 #N") because they apply here unchanged.
Entries #10 onwards were learned on 5.00. Read the full 4.9 history when porting a 4.9 plugin.

| # | Mistake | Rule |
|---|---|---|
| 1 | *(4.9 #110)* Asked to "add the solution to the repository", the agent committed and tried to push; when the push was blocked it tried again by other routes. The user handles git. | **Prepare the repository (`.gitignore`, secret scrub, file review) and hand over the exact git commands; never commit or push, and never retry a push through another tool or the user's terminal.** |
| 2 | *(4.9 #4)* Put `#region Utilities` after the public method regions. | Region order is **Fields → Ctor → Utilities → public method regions**; helpers come before the methods that use them (`CLAUDE.md` §7). |
| 3 | *(4.9 #5, #2)* Reused a Razor helper that no longer existed in the target nop version; the build passed and the page died on first request. | **Razor compiles on first request — a clean build proves nothing about views.** Check every framework type and helper a ported view uses against the target version, then load the page. |
| 4 | *(4.9 #74)* A `.js` edit seemed to have no effect; WebOptimizer's disk cache served the old bundle across rebuilds and restarts. | **A `.js` or `.css` edit needs `Presentation/Nop.Web/wwwroot/bundles` emptied** — not a rebuild, not a restart (`CLAUDE.md` §9.11). |
| 5 | *(4.9 #73)* A `let` in bundled admin JS was moved by the minifier out from under the hoisted function that read it — a silent no-op only in the minified bundle. | Use `var` for a closure variable read by a hoisted inner function in bundled JS (`CLAUDE.md` §9.11). |
| 6 | *(4.9 #75)* A partial that bound delegated handlers to a long-lived container stacked them on every render (one confirm became four). | A partial that binds delegated handlers to an element it does not own calls `.off('.ns')` first and namespaces every handler (`CLAUDE.md` §9.12). |
| 7 | *(4.9 #77)* Measured a page that was still running a debugging probe from an earlier session. | **Reload the page before every verification round.** When live behaviour contradicts the source, first check the page runs the source — read the function back (`fn.toString()`). |
| 8 | *(4.9 #104)* Claimed what a page shows "on load" from the page after its scripts had run. | Check "on load" on the raw server response (`fetch` + parse) and the scripted page separately; a difference between the two is the finding. |
| 9 | *(4.9 #103)* Signed the admin out of the shared browser session while testing. | The browser session is the user's. Before `/logout` or any form that can sign someone in, check who is signed in; never sign the user out or change their credentials. |
| 10 | Porting the SSO plugin, admin JS read `data.success` / `data.message`; nop 5.00 admin JSON is PascalCase, so every value was `undefined` and the import, test and badge buttons silently did nothing. | **nop admin JSON is PascalCase** (`data.Success`). Check every property a ported script reads against the JSON the action actually returns (`CLAUDE.md` §10.5). |
| 11 | Porting the SSO plugin, packages compiled against newer `Microsoft.IdentityModel.*` than nop 5.00 ships; it built and would have failed at runtime. | **Pin plugin packages to the versions the host ships**, and pick third-party packages whose dependency floor is at or below them. "It compiles" does not prove runtime compatibility (`CLAUDE.md` §10.5). |
| 12 | A store-scope save on the SSO Configure page wrote the hidden system-wide fields as empty and 0, blanking settings the page did not show (D6, 2026-10-19). | **A form saves only what it displays.** When a page shows different cards per scope, the save path branches the same way; test a save at each scope and read the other scope's values back. |
| 13 | `sed` with `&` in the replacement text (a heading containing "&") pasted the matched line into the result and garbled four headings (2026-10-20). | Escape `&` (and `\`) in `sed` replacement text, or use the Edit tool; grep the result after any bulk `sed`. |
