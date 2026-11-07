# Enemy Health Bars

Standalone health bars for enemies in SULFUR. Hit an enemy and a bar appears above it, fades after a few seconds, and goes away when the enemy dies.

The mod does not change damage, enemy stats, or loot. It only reads what the game already knows and draws it.

## Bar styles

Five looks, all shown at the moment an enemy takes a hit: the red part is the health left, and the pale part is the health just lost.

![The five bar styles](https://raw.githubusercontent.com/ryuka-dev/Enemy-Health-Bars/main/docs/styles-default.png)

Sulfur and Rounds follow the game's UI color, so they change with the HUD in areas like the desert.

![The five bar styles in the desert colors](https://raw.githubusercontent.com/ryuka-dev/Enemy-Health-Bars/main/docs/styles-desert.png)

## In combat

![Sulfur style against several enemies](https://raw.githubusercontent.com/ryuka-dev/Enemy-Health-Bars/main/docs/sulfur-combat-2.gif)

## [Watch every style in action on GitHub](https://github.com/ryuka-dev/Enemy-Health-Bars/blob/main/docs/GALLERY.md)

8 more clips, covering each style and more fights. They are GIFs of 3 to 18 MB each, about 76 MB in total, so the page can take a while on a slow connection.

## How it behaves

- A bar shows after an enemy loses health and fades about six seconds after the last hit.
- You need a clear line of sight. Enemies behind walls have no bar.
- Enemies the game is hiding stay hidden: invisible enemies, disguised ambushers, and ghosts while they are faded out.
- Bosses keep the game's own boss bar and get no second one.
- Allies, charmed enemies, shopkeepers and other friendly characters have no bar.
- Bars disappear in menus, the inventory, dialogue, cutscenes and on the death screen.
- Enemy names use the game's own translations.

## Settings

Change these in `BepInEx/config/ryuka.sulfur.enemy_health_bars.cfg`, or in game through SULFUR Config if you have it installed.

| Setting | Default | What it does |
|---|---|---|
| Display mode | Damaged | **Damaged** shows bars only on enemies that lost health. **Nearby** shows bars on every enemy you can see in range, up to 32 at once, nearest first. |
| Bar style | Sulfur | **Sulfur** matches the game's own health bar and follows its UI color. **Rounds** is a row of ten rings like the ammo gauge. **Ichor** is a run of dripping ink. **Bone** has a skull at the head of the bar. **Classic** is a plain bar. |
| Max distance | 30 | Farthest distance in meters at which a bar is shown (5 to 100). |
| Bar scale | 1 | Size of the bars and names (0.5 to 2). |
| Show name | On | Shows the enemy's name above its bar. |

## Compatibility

- Needs only BepInEx. SULFUR Config is optional.
- **Battle Improvements** has its own enemy health bar. If you run both, turn off its health bar (`EnableHealthBar`) or each enemy shows two bars.
- Works alongside Damage Sparks.
- SULFUR Together: everything is local to your game. Bars follow the health the host sends, so on a joining player they can lag slightly behind. Enemies that are invisible until they notice you may not get a bar on a joining player.

## Issues

Report problems at https://github.com/ryuka-dev/Enemy-Health-Bars/issues.
