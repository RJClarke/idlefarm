# Cast & Copy

_Generated 2026-10-05 00:27 from `Assets/Resources/LetterCatalog.asset` — don't edit this file by hand, it gets overwritten._

**To change copy:** select `LetterCatalog` in the Project window and edit it in the Inspector (Cast / Letters / Tips), then run **Farm Game > Narrative > Write Copy Reference** to refresh this page. Or just ask Claude.

`{farmName}` in a letter is replaced with the player's farm name. Emoji don't render on Android — keep copy to plain text.

## Cast

| Character | Who they are | Letters |
|---|---|---|
| **Mayor Bramble** | Mayor of the valley town, and the player's first friend. | 9 |
| **Pippa the Tinker** | Inventor who builds gadgets for farms (Compost Bay, equipment). | 1 |
| **Harry the Carpenter** | Runs the carpenter's shop at the market: tools (axe, fishing pole) and buildings. | 4 |
| **Old Finch** | Retired fisherman who knows every inch of the lake. Also posts Town Requests. | 3 |
| **Marta** | Runs the jam stall; the town's preserves expert. Also posts Town Requests. | 1 |
| **Pip** | Kid with a picky pet rabbit. Posts Town Requests (no mail yet). | 0 |
| **Hazel** | Runs the seed stall at the Market. Sells every crop's seed packet. | 3 |

## All letters at a glance

| Letter | From | Arrives when | Gift | Button |
|---|---|---|---|---|
| Welcome to {farmName}! | Mayor Bramble | sent directly by code (e.g. right after naming the farm) | Radish seed packet + 50 Coins | — |
| You can build a Compost Bay! | Pippa the Tinker | research unlocks `composting_basics` | — | Go to Equipment |
| Your first harvest! | Mayor Bramble | run #1 ends _(new farms only)_ | — | See Farm Upgrades |
| Those trees won't chop themselves | Harry the Carpenter | run #2 ends _(new farms only)_ | — | Visit Harry's Shop |
| Timber! | Harry the Carpenter | the first tree is chopped down _(new farms only)_ | — | — |
| The fish are biting | Old Finch | run #3 ends, or the axe is bought _(new farms only)_ | — | Visit Harry's Shop |
| Your first catch | Old Finch | the first fish is caught _(new farms only)_ | — | — |
| Jam season! | Marta | research unlocks `cannery_unlocked` | — | Visit Harry's Shop |
| A proper smokehouse | Old Finch | research unlocks `smokehouse_unlocked` | — | Visit Harry's Shop |
| Your Cannery is ready | Harry the Carpenter | cannery is built | — | — |
| Your Smokehouse is ready | Harry the Carpenter | smokehouse is built | — | — |
| The town is grateful | Mayor Bramble | the first Town Request is delivered _(new farms only)_ | — | Open the Barn |
| A new friend on the farm | Mayor Bramble | the first animal is unlocked _(new farms only)_ | — | See Animals |
| More room to grow | Mayor Bramble | Field 2 is bought _(new farms only)_ | — | Choose Seeds |
| The talk of the valley | Mayor Bramble | a run lasts 1h _(new farms only)_ | 25 Gems | — |
| A farm to be proud of | Mayor Bramble | a run lasts 3h _(new farms only)_ | 50 Gems | — |
| Fresh seeds at my stall | Hazel | run #1 ends _(new farms only)_ | — | Visit Hazel's Stall |
| These ones keep giving | Hazel | the first regrowing crop is bought _(new farms only)_ | — | — |
| The town could use a hand | Mayor Bramble | run #4 ends _(new farms only)_ | — | See Requests |
| One crop, two fields? | Hazel | Field 2 is bought _(new farms only)_ | — | Visit Hazel's Stall |
| A little thank-you | Mayor Bramble | `welcome_basket_done` | — | — |

## Letters by character

### Mayor Bramble

**Who:** Mayor of the valley town, and the player's first friend.  
**Voice:** Warm, proud, a touch formal. Speaks for the whole town and celebrates milestones, sometimes with a small gift. Opens "Dear {farmName}," and signs "- Mayor Bramble".

#### "Welcome to {farmName}!"  `welcome`

- **Arrives:** sent directly by code (e.g. right after naming the farm)
- **Gift:** Radish seed packet + 50 Coins

> Dear {farmName},
>
> Welcome to the valley! This old farm has sat quiet for years, and we're all so glad someone is bringing it back to life.
>
> I've left a sack of radish seeds in your shed. They're quick to grow and hard to get wrong - just the thing for a first harvest. Choose a field, and your helpers will plant, water and harvest for you.
>
> Every harvest earns Money to buy more seeds, plus Coins you get to keep. Hazel at the seed stall in the Market has more kinds when you're ready - here's a little something toward your first packet.
>
> - Mayor Bramble

#### "Your first harvest!"  `first_run_done`

- **Arrives:** run #1 ends _(new farms only)_
- **Button:** See Farm Upgrades

> Dear {farmName},
>
> What a sight - crops growing on that old farm again! The whole town is talking.
>
> Those Coins you earned are yours to keep. Spend them on farm upgrades and your next run will go even further. If you're not sure where to start, a bigger field is never a bad idea.
>
> - Mayor Bramble

#### "The town is grateful"  `first_request`

- **Arrives:** the first Town Request is delivered _(new farms only)_
- **Button:** Open the Barn

> Dear {farmName},
>
> Word gets around fast in a town this size, and folks are singing your praises! Every request you fill earns you Reputation.
>
> Fill the Reputation bar and you'll earn a point to spend at your Barn, making your whole farm better at what it does.
>
> - Mayor Bramble

#### "A new friend on the farm"  `first_animal`

- **Arrives:** the first animal is unlocked _(new farms only)_
- **Button:** See Animals

> Dear {farmName},
>
> I hear you've got some company out there! Animals do more than keep you company - each one changes how your farm runs.
>
> Open your animals menu to put your new friend to work.
>
> - Mayor Bramble

#### "More room to grow"  `field_two`

- **Arrives:** Field 2 is bought _(new farms only)_
- **Button:** Choose Seeds

> Dear {farmName},
>
> A second field! You're turning that old place into a real farm.
>
> Don't forget to pick seeds for it before your next run - an empty field grows nothing.
>
> And a little secret from someone who's watched plenty of farms grow: a helper can only be in one place at a time. If your new field is slow to get going, a second pair of hands works wonders. A quicker helper or a sprinkler helps too - less time hauling water means more time planting!
>
> - Mayor Bramble

#### "The talk of the valley"  `long_run_1h`

- **Arrives:** a run lasts 1h _(new farms only)_
- **Gift:** 25 Gems

> Dear {farmName},
>
> A whole hour of harvests without a break! Folks in town can hardly believe it.
>
> Please accept this small token from all of us. Keep it up!
>
> - Mayor Bramble

#### "A farm to be proud of"  `long_run_3h`

- **Arrives:** a run lasts 3h _(new farms only)_
- **Gift:** 50 Gems

> Dear {farmName},
>
> Three whole hours! Travelers are stopping in town just to ask about your farm.
>
> The council voted, and this gift is from all of us. Well earned.
>
> - Mayor Bramble

#### "The town could use a hand"  `town_board_intro`

- **Arrives:** run #4 ends _(new farms only)_
- **Button:** See Requests

> Dear {farmName},
>
> Word's getting around about your farm! A few of us in town could use a hand now and then, so we post what we need on the community board by the Market.
>
> Help out, and folks won't forget it. Every delivery earns you Reputation, and a good name in this valley opens doors - your Barn will show you how.
>
> I've pinned the first request myself.
>
> - Mayor Bramble

#### "A little thank-you"  `town_gift`

- **Arrives:** `welcome_basket_done`

> Dear {farmName},
>
> The whole town loved your basket. We'll leave a little something at your farm now and then.
>
> - Mayor Bramble

### Pippa the Tinker

**Who:** Inventor who builds gadgets for farms (Compost Bay, equipment).  
**Voice:** Excitable and quick, loves a clever contraption. Opens "Hey {farmName}!" and signs "- Pippa".

#### "You can build a Compost Bay!"  `compost_bay_unlock`

- **Arrives:** research unlocks `composting_basics`
- **Button:** Go to Equipment

> Hey {farmName}!
>
> Heard you've been reading up on composting. Good news - I can put together a Compost Bay for you now. Swing by the shop and grab one; your soil will thank you.
>
> Oh! And ask Hazel about corn. Those big stalks make twice the compost when a plant doesn't make it.
>
> - Pippa

### Harry the Carpenter

**Who:** Runs the carpenter's shop at the market: tools (axe, fishing pole) and buildings.  
**Voice:** Friendly, plainspoken tradesman with a folksy streak ("Howdy", "mighty"). Practical advice about wood and building. Signs "- Harry".

#### "Those trees won't chop themselves"  `axe_offer`

- **Arrives:** run #2 ends _(new farms only)_
- **Button:** Visit Harry's Shop

> Howdy {farmName},
>
> Harry here, the carpenter down at the market. Noticed the woods behind your place are getting mighty overgrown. That timber's yours for the taking!
>
> Come see me and I'll set you up with an axe. Wood sells at the rack, and a good builder always needs more of it.
>
> - Harry

#### "Timber!"  `first_tree`

- **Arrives:** the first tree is chopped down _(new farms only)_

> {farmName},
>
> Heard that one fall from here! Nice swing.
>
> Your wood stacks up on the rack by the woods. Sell some for Coins, but hang on to a pile, too. Before long you'll want it for fuel, and I'll have buildings to put up for you.
>
> - Harry

#### "Your Cannery is ready"  `cannery_built`

- **Arrives:** cannery is built

> {farmName},
>
> All built, and sturdy as they come! Load your Cannery up with crops, then keep the firebox stocked with wood.
>
> If the fire goes out, nothing spoils. The jars just wait for you.
>
> - Harry

#### "Your Smokehouse is ready"  `smokehouse_built`

- **Arrives:** smokehouse is built

> {farmName},
>
> Smokehouse is up! Old Finch already came by to inspect the chimney.
>
> Put raw fish in the smoker and keep the fire fed with wood. If it burns out, the fish just wait.
>
> - Harry

### Old Finch

**Who:** Retired fisherman who knows every inch of the lake. Also posts Town Requests.  
**Voice:** Slow, dry, few words; fond of the lake and of patience. Opens "Well now," or just the farm name. Signs "- Old Finch".

#### "The fish are biting"  `pole_offer`

- **Arrives:** run #3 ends, or the axe is bought _(new farms only)_
- **Button:** Visit Harry's Shop

> Well now, {farmName}.
>
> That lake past your farm is full of perch, and nobody's cast a line in it for years. Shame, that.
>
> Harry keeps a fishing pole or two in his shop. Get yourself one and come down to the water. Patience is the whole trick of it.
>
> - Old Finch

#### "Your first catch"  `first_fish`

- **Arrives:** the first fish is caught _(new farms only)_

> Ha! Knew you had it in you, {farmName}.
>
> Here's something an old fisherman will tell you for free: watch for the whirlpools. That's where the fish gather, and a line cast there gets a bite much quicker.
>
> - Old Finch

#### "A proper smokehouse"  `smokehouse_unlock`

- **Arrives:** research unlocks `smokehouse_unlocked`
- **Button:** Visit Harry's Shop

> {farmName},
>
> So you've learned the old smoking ways. Good. Raw fish sells, but smoked fish? That's worth several times as much.
>
> Harry can build you a Smokehouse near the lake. Keep the fire fed with wood and the fish will do the rest.
>
> - Old Finch

### Marta

**Who:** Runs the jam stall; the town's preserves expert. Also posts Town Requests.  
**Voice:** Cheerful, chatty and food-obsessed; always angling for a jar. Opens "Hello {farmName}!" and signs "- Marta".

#### "Jam season!"  `cannery_unlock`

- **Arrives:** research unlocks `cannery_unlocked`
- **Button:** Visit Harry's Shop

> Hello {farmName}!
>
> Marta here, from the jam stall. Word is you've been learning to preserve, and I couldn't be happier!
>
> Have Harry build you a Cannery. Your crops can go into jars instead of being sold, and a good jar is worth far more than the fruit it came from.
>
> Hazel's got strawberry seeds in now, too - they make the quickest jam there is!
>
> Save me a jar of strawberry?
>
> - Marta

### Pip

**Who:** Kid with a picky pet rabbit. Posts Town Requests (no mail yet).  
**Voice:** Eager and a little dramatic about the rabbit.

_No letters yet._

### Hazel

**Who:** Runs the seed stall at the Market. Sells every crop's seed packet.  
**Voice:** Warm, practical, loves a bargain and a good tip about what grows well. Opens "Hi {farmName}!" and signs "- Hazel".

#### "Fresh seeds at my stall"  `seed_stall_intro`

- **Arrives:** run #1 ends _(new farms only)_
- **Button:** Visit Hazel's Stall

> Hi {farmName}!
>
> Hazel here - I run the seed stall at the Market. Saw your radishes come up. Lovely work!
>
> When you're ready to try something new, come see me. Carrots are next to nothing, and they sell for nearly twice as much.
>
> Once you buy a packet, that crop is yours to plant every run after.
>
> - Hazel

#### "These ones keep giving"  `regrow_bought`

- **Arrives:** the first regrowing crop is bought _(new farms only)_

> Hi {farmName}!
>
> Good choice! That one's a regrower. Pick it, and as long as the plant survives, it grows back and gives you another harvest - no new seed needed.
>
> Keep it watered and keep the pests off, and it'll pay for itself many times over.
>
> - Hazel

#### "One crop, two fields?"  `extra_packets`

- **Arrives:** Field 2 is bought _(new farms only)_
- **Button:** Visit Hazel's Stall

> Hi {farmName}!
>
> A second field - congratulations! You can plant a different crop in each field, or double down on one.
>
> Every packet you own lets a crop grow in one field. Want corn in both fields to pile up compost, or radishes everywhere to fill a big order? Pick up a second packet at my stall.
>
> Mixing crops spreads your luck - deer and crows each have their favorites!
>
> - Hazel

## Tutorial steps & how-to tips

Shown by the spotlight overlay, once each, only on farms named after onboarding shipped (Dev Tools > **Replay Onboarding** opts any save back in).

| Shows when | Text | id |
|---|---|---|
| First session 1/5: right after naming the farm (spotlights the mailbox) | Mail arrives here. Tap to read your welcome letter from the Mayor. | `onboarding_mailbox` |
| First session 2/5: closing the mailbox after the welcome letter (spotlights Field) | The Mayor's seeds are waiting in your shed. Tap here to choose what to plant. | `onboarding_field` |
| First session 3/5: seed picker open (spotlights the whole picker) | Tap a seed to plant it in your field, then tap Save. | `onboarding_pick_seeds` |
| First session 4/5: after saving seeds (spotlights Start Run) | Your helpers will plant, water and harvest for you. Tap Start Run and watch your farm grow! | `onboarding_start_run` |
| First session 5/5: the first run starts | Each harvest earns Money to buy more seeds during this run. / Coins are yours to keep, even after the run ends. Spend them to grow your farm! | `onboarding_money_note` |
| Any run: first time a helper can't afford a seed bag (points at the seed counter) | You're out of Money for seeds! Every harvest earns more, but seed bags cost more the longer a run goes. When you can't buy seeds and nothing is growing, the run ends. | `tip_out_of_money` |
| First time the Farm upgrades menu opens | Spend your Coins here to make every run better. A bigger field is a great first buy! | `tip_farm_upgrades` |
| First time the Quests menu opens | Finish daily quests to earn Gems. New quests arrive every day. | `tip_quests` |
| First time the daily rewards calendar opens | Every visit moves you one gift along the week, and each gift is bigger than the last. Collect all 7 for a bonus! | `tip_daily_rewards` |
| First arrival at the Market | Welcome to the market! Look around - shops here sell seeds, tools and buildings. Check the notice board for townsfolk who need a hand. | `tip_market` |
| First arrival at the Woods without an axe | These trees are yours to chop, but you'll need an axe first. Harry the Carpenter sells one at the market. | `tip_woods_no_axe` |
| First arrival at the Woods with an axe | Tap a tree to swing your axe. Each chop knocks loose some wood, and chopped trees grow back over time. | `tip_woods` |
| First time the Wood Rack opens | Your wood piles up here. Sell it for Coins, or save it to fuel your buildings later. | `tip_wood_rack` |
| First arrival at the Lake with a fishing pole | Press and hold to aim your cast, then let go to throw. When a fish bites, tap or hold to reel it in. Whirlpools hold extra fish! | `tip_lake` |
| First fish caught | Nice catch! Fish go to your pantry. Sell them raw, or smoke them in a Smokehouse for much more. | `tip_first_catch` |
| First time the Smokehouse opens | Load raw fish into the smoker, then keep the fire burning with wood. If the fire goes out, nothing spoils - smoking just pauses. | `tip_smokehouse` |
| First time the Cannery opens | During a run, some harvested crops go into your jars instead of being sold. Keep the firebox full of wood and they'll become preserves worth far more. | `tip_cannery` |
| First compost earned | Your Compost Bay turned a lost crop into compost! Spend compost to speed up research. | `tip_compost` |
| First time Research opens | Pick a topic to study. Research finishes over real time, even while you're away, and gives your farm permanent boosts. | `tip_research` |
| First time the Animals menu opens | Animals change how your farm plays. Unlock them with Gems from quests and daily gifts, then equip one for your runs. | `tip_animals` |
| First time the Town Requests board opens | Townsfolk post what they need here. Deliveries earn Reputation, and a full bar gives you a point to spend at your Barn. Switch to Collect during a run to keep harvests for requests. | `tip_town_requests` |
| First time the Barn (Farm Level) opens | Spend skill points to make your farm better at what it does. Tap a numbered milestone to see what it unlocks. | `tip_barn` |
| First run after the town-board letter (points at the Collect / Sell switch) | Sell turns each harvest into Money right away, to keep your run going. Collect keeps harvests as items, for town requests and your jars. | `tip_collect_sell` |
| First time the Farmer's Almanac opens | Everything you've discovered is written down here. Tap any page to see its numbers, including how much your upgrades have added. | `tip_almanac` |
| First time Hazel's seed stall opens | Buy a seed packet once and that crop is yours to plant every run. The mystery packets are still waiting to be discovered! | `tip_seed_stall` |
| First regrowing crop harvested in a run | This crop regrows! As long as the plant survives after it's picked, it grows more - no new seed needed. | `tip_regrow` |
| Barn opened with a skill point to spend (points at a + button) | You have a point to spend! Tap + to level up a skill. Each level makes your farm a little better at that job. | `tip_barn_spend` |
| Hazel's stall opened once a crop can take a 2nd packet (more fields than packets) | Each packet lets a crop grow in one field. Buy another to plant it in more fields at once - great for going all-in on compost, jam or a big town order. | `tip_extra_packet` |
| A run where a field with a crop chosen is still bare after a few minutes | This field is still waiting its turn - your helper has its hands full with your other crops! Another helper, a speedier one, or a sprinkler would lend a hand. | `tip_idle_field` |
| Free Gift chest first unlocks (after the Welcome Basket letter is read) | A gift every 30 minutes. This one's on us! | `tip_town_gift` |

## Farmer's Almanac pages

The "why pick this" blurb at the top of each Almanac page. Stats and tags under it are generated from game data. Equipment and animals without an entry here use their own description.

| Page | Blurb | id |
|---|---|---|
| Radish page | In and out of the ground before pests notice. Tiny payout, lightning fast. | `almanac_crop_radish` |
| Carrot page | Buried where crows can't reach - but deer will dig for them. | `almanac_crop_carrot` |
| Corn page | Tall stalks shrug off deer, and the harvest waits a while for you. Crows can't resist it. | `almanac_crop_corn` |
| Tomato page | Sturdy vines that keep on giving. Thirsty, though, and deer know where to find them. | `almanac_crop_tomato` |
| Strawberry page | Every bird in the valley knows when these are ripe. Makes the quickest jam at the Cannery. | `almanac_crop_strawberry` |
| Blueberry page | Slow to settle in, then fruits again and again. Thirsty, and a crow's favourite - but deer mostly leave it be. | `almanac_crop_blueberry` |
| Green Beans page | Quick to crop and quick to grow back. Tender, though, and deer can't get enough of them. | `almanac_crop_green_beans` |
| Green Pepper page | Tough and long-lived. Crows ignore the green fruit, but deer nibble the leaves - plant it where crows are the problem. | `almanac_crop_green_pepper` |
| Red Pepper page | The green pepper's twin, left to ripen. Too hot for deer, but crows can't resist the red - plant it where deer are the problem. | `almanac_crop_red_pepper` |
| Deer page | Deer wander in from the woods and graze on your fields. They go easy on seeds but hit young plants hardest. Fences keep them out, and a dog will chase them off. | `almanac_pest_deer` |
| Crows page | Crows swoop down and peck at your crops - fresh seeds and ripe berries most of all. A scarecrow scares them off. | `almanac_pest_crow` |

