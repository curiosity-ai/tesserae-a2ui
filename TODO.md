# TODO

Running list of what is not done or is rough. Nothing here is hidden by a green test.

## Requires an unpublished Tesserae
- `Video`, `AudioPlayer` and a vertical `Divider` use `Tesserae.VideoPlayer`, `AudioPlayer` and `VerticalSeparator`, which are in Tesserae after 2026.11.73499. Bump the Tesserae version in both csprojs once a build containing them is published (the themes pin follows).

## Protocol
- v0.9 / v0.9.1 / v0.8 messages are not accepted (a message must be v1.0 shaped; `version` is not enforced).
- `allowedParents` / `allowedChildren` composition constraints (`UNALLOWED_PARENT` / `UNALLOWED_CHILD`) are not evaluated; the basic catalog declares none.
- Function calls the catalog does not implement are routed to the agent from **actions** only. A binding or a check that names such a function reports
  `EXPRESSION_ERROR`; there is no shared in-flight call or cache for reactive agent calls.
- Catalog JSON Schemas are not loaded: catalogs are registered in code, and `agent_capabilities` / `renderer_capabilities` negotiation is left to the transport.
- Incoming messages are checked structurally (envelope shape, ids, `theme` rejected) but not validated against the JSON Schemas.
- The regex safety check implements the length cap, the forbidden constructs, and the nested-unbounded-quantifier and alternation rules; the adjacent
  overlapping-quantifier rule (rule 4) is not implemented.

## Components
- `Slider` moves in whole steps (a decimal range is mapped onto up to 100 positions).
- `Icon` maps the 60 catalog names onto the closest `UIcons` glyphs (`starHalf` uses a sharp half star, `phone` a flipped handset); there is no icon for `volumeUp` other than `Volume`.
- `DateTimeInput` stores what the user picked as local ISO 8601 text with no time zone.
- `TextField` `number` writes a string, as the catalog types `value` as `DynamicString`.
- A rebuilt component (its definition changed) loses its own transient state (a half-typed field is kept in the data model, so it survives; a Tabs selection does not).

## Catalogs
- The `mcp` and `vegachart` catalogs in `.reference/catalogs/` are not implemented (a Flint/Mermaid mapping would suit the chart one).
- The `tesserae` skill in Tesserae does not know about this package yet (`Tesserae.A2UI/skills/` is not shipped).

## Gallery
- Examples that load remote images (posters, album art) show a broken image offline.
- Zeitung's drop caps apply to every Markdown paragraph, including one-word cells in a list row.
