The playable campaign has six levels, plus the existing tutorial.

| Displayed level | Scene | Content |
| --- | --- | --- |
| 1 | Level0 | Original first level, unchanged |
| 2 | ShadowLevel | Finn's smaller shadow puzzle |
| 3 | Level2 | Original third level, unchanged |
| 4 | LinkedMaps | Original second map below original fourth map |
| 5 | Level4 | Original fifth level, unchanged |
| 6 | ShadowLevel 2 | Finn's large shadow maze |

The existing tutorial now teaches Q / the mobile CLONE button with a pressure
button and a full-height gate after its first automatic flip. The separate
ShadowTutorial scene has been removed.

LinkedMaps preserves both source wall layouts. The upper map is 24 units above
the lower one, leaving a five-unit gap. Two stars, the original spawn, and the
only exit are below; three stars are above. A two-way portal connects the maps.
Both sections flip on the same timer, each around its own center. Follow-camera
bounds and recorded routes stay with their section; preview/completion frame
both maps. Original Level1 and Level3 remain source assets outside build settings.

Rebuild with Tools > Campaign > Build Six-Level Campaign. Run Unity batch checks
with `-executeMethod CampaignChecks.Run -uiReviewSmoke`,
`-executeMethod ReferenceUiSmoke.Run -uiReviewSmoke`, and
`-executeMethod FinnLevelsChecks.Run -uiReviewSmoke`. Progression tests run in
EditMode. `python Tools/validate_linked_maps.py` checks source-wall preservation
and ball-clear connectivity; it does not prove a timed jump route. CampaignChecks
tests real trigger behavior using fixture relocations, while the tutorial smoke
uses keyboard input and physics.

Existing saves retain completions for unchanged levels and remap Finn's two maps
from the earlier eight-level import. Replaced maps and the removed tutorial do
not mark the new puzzles complete.

The level-selection path uses the original flowing curves and original first
five node positions; the sixth node sits on the final curve.

Permanent starting lights unlock once each: tutorial completion grants the
first, displayed level 1 grants the second, and displayed level 3 grants the
third. Other levels grant none. Stars still add one live charge each with no
gameplay cap; live charges reset to the earned starting allowance on a new run.
Tutorial replays start at zero to preserve the first-star light lesson. Older
saves did not record tutorial completion, so that reward requires completing
the tutorial after this update.
