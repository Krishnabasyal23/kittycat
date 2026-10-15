FLOWER & PLANT PACK  (v1.0)  by Kebap Guy  -  Documentation
32 plant types - 82 designs - 7 flower colors - 4 seasons - animated - 2 sizes
pixel art for 16x16 tile grids: top-down RPGs, farming sims and adventure games

Requires Unity 2022.3 or newer.

============================================================
QUICK START
============================================================
1. Open Demo/Demo.unity to see every plant swaying.
2. Static plant: drag a sprite from Sprites/<Category>/<type> into your scene.
3. Animated plant: drag a prefab from Prefabs/Animated/<Category>/<type>
   into your scene. It starts swaying automatically.

<Category> = Flowers, Grass, Plants, Bushes

============================================================
CONTENTS
============================================================
786 static sprites + 786 animated plants
(every static sprite also comes as a looping wind-sway animation)

Sprites/<Category>/<type>            786 static sprites
Sprites/Animated/<Category>/<type>   786 animation sprite sheets, already
                                     sliced into 12 frames each
Animations/<Category>/<type>         786 looping animation clips + 786
                                     Animator Controllers (120 ms per frame)
Prefabs/Animated/<Category>/<type>   786 ready-to-use animated plants
                                     (Sprite Renderer + Animator)
Demo/Demo.unity                      demo scene

Types (32 types, several different designs of each).
File name prefix, description, number of designs, variants, size (small size):
  FLOWERS
    flower_tulip       Tulip                     x3  7 colors  32x32  (small 16x16)
    flower_daisy       Daisies                   x3  7 colors  32x32  (small 16x16)
    flower_rose        Rose                      x2  7 colors  32x32  (small 16x16)
    flower_sunflower   Sunflower                 x3  7 colors  32x48  (small 16x32)
    flower_lavender    Lavender                  x3  7 colors  32x32  (small 16x16)
    flower_poppy       Poppies                   x3  7 colors  32x32  (small 16x16)
    flower_bluebell    Bluebells                 x3  7 colors  32x32  (small 16x16)
    flower_dandelion   Yellow pompom flowers     x3  7 colors  32x32  (small 16x16)
    flower_wildflowers Mixed wildflowers         x2  1 color   32x32  (small -)
    flower_waterlily   Water lily                x3  7 colors  32x32  (small 16x16)
  GRASS
    grass_tuft         Grass tuft                x3  4 seasons 32x32  (small 16x16)
    grass_tall         Tall grass                x2  4 seasons 32x32  (small 16x16)
    grass_flowering    Grass with small flowers  x2  7 colors  32x32  (small 16x16)
    grass_cattail      Cattails (reeds)          x3  4 seasons 32x48  (small 16x32)
    grass_wheat        Wheat                     x3  4 seasons 32x32  (small 16x16)
    grass_clover       Clover                    x2  4 seasons 32x32  (small 16x16)
  PLANTS
    plant_leafy        Broad-leaf plant          x3  4 seasons 32x32  (small 16x16)
    plant_longleaf     Long-leaf plant           x2  4 seasons 32x32  (small 16x16)
    plant_fern         Fern                      x4  4 seasons 32x32  (small 16x16)
    plant_thistle      Thistle                   x3  4 seasons 32x32  (small 16x16)
    plant_vine         Hanging vine              x3  4 seasons 32x32  (small 16x16)
    plant_sprout       Sprout / seedling         x1  4 seasons 32x32  (small 16x16)
    plant_lilypad      Lily pads                 x3  4 seasons 32x32  (small 16x16)
    plant_cactus       Barrel cactus             x2  1 color   32x32  (small 16x16)
    plant_succulent    Succulent                 x3  1 color   32x32  (small 16x16)
    plant_mushroom     Red mushrooms             x3  1 color   32x32  (small 16x16)
  BUSHES
    bush_round         Round bush                x3  4 seasons 32x32  (small 16x16)
    bush_large         Large bush                x3  4 seasons 48x48  (small 32x32)
    bush_flowering     Flowering bush            x2  7 colors  32x32  (small 16x16)
    bush_berry         Berry bush                x2  7 colors  32x32  (small 16x16)
    bush_hedge         Hedge block               x1  4 seasons 32x32  (small 16x16)
    bush_wild          Wild shrub                x1  4 seasons 32x32  (small 16x16)

============================================================
VARIANTS
============================================================
Flowers - plus the flowering grass, flowering bush and berry bush -
come in 7 colors:
  red, pink, yellow, orange, white, blue, purple
  (only the petals / berries change color, the leaves stay green)

Grass, plants and bushes come in 4 seasons:
  spring   fresh green with tiny blossoms
  summer   full green
  autumn   golden dry grass, golden ferns; orange-red bushes and vines
  winter   frosted, snow-covered leaves
The leaf colors match the Seasonal Tree Pack season by season.

Barrel cactus, succulent, red mushrooms and mixed wildflowers come in one
version.

============================================================
FILE NAMES
============================================================
  <type>_<design>_<variant>.png                flower_tulip_02_purple.png
  <type>_<design>_small_<variant>.png          bush_berry_01_small_blue.png
  <type>_<design>[_small]_<variant>_sway       grass_tall_02_autumn_sway
                                               (sprite sheet, clip, controller
                                               and prefab share this name)

Every design exists in 2 sizes (except mixed wildflowers). The "small"
version is the same design at half size with its own outline, not a
blurry downscale.

============================================================
IMPORT SETTINGS (already set up)
============================================================
- Pixels Per Unit = 16: one 16x16 tile is 1 Unity unit.
- Filter Mode = Point, Compression = None, no mipmaps: pixels stay sharp.
- Pivot = Bottom Center: every sprite is bottom-aligned and horizontally
  centered on its canvas, so plants line up on your tile grid.
- Every canvas is a multiple of 16:
    16x16 = 1x1 tile (small flowers and grass)    32x32 = 2x2 tiles
    16x32 = 1x2 tiles (small sunflower, cattails) 32x48 = 2x3 tiles
    48x48 = 3x3 tiles (large bush)
- PNG with true alpha transparency, no semi-transparent pixels.
- All sprites share one limited palette, light comes from the top left
  and every sprite has a dark outline.
- Use the sprites at their original size - don't scale them down.

============================================================
ANIMATIONS
============================================================
Every animated plant is a 12-frame loop at 120 ms per frame (about 8 fps) -
the same timing as the Seasonal Tree Pack, so trees and plants sway
together.

  sway   stems, leaves and flowers sway in the wind; the base stays still.
         Rigid or floating plants (cactus, succulent, mushrooms, lily pads,
         water lily, hedge) only move gently at the top.

Animated plants use the same design, size and base position as their
static version, so you can mix both in one scene.

============================================================
RENDER PIPELINES
============================================================
These are plain sprites with no custom materials or shaders, so they work
in the Built-in Render Pipeline and in URP (2D Renderer and Universal
Renderer). The prefabs and the demo scene use the Sprites-Default material.
Using URP 2D lights? Set the Sprite Renderer material to
Sprite-Lit-Default so the plants react to your lights.

============================================================
LICENSE
============================================================
Licensed under the Standard Unity Asset Store EULA - see LICENSE.txt.

============================================================
CREDITS
============================================================
Created with PixelLab (AI image generation), then cleaned up, recolored
(flower colors and seasons) and animated with a custom pixel-art pipeline
and edits in Aseprite.

Questions or feedback? Contact Kebap Guy through the publisher page
on the Unity Asset Store.
