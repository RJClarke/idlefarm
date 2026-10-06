/// <summary>
/// Starting copy for the cast, letters and how-to tips. The LIVE copy is the LetterCatalog asset
/// (Resources/LetterCatalog.asset) — edit text there (Inspector) and it wins. These defaults only
/// seed entries the asset doesn't have yet (Farm Game > Narrative > Seed Missing Copy never
/// overwrites an existing id), and back tips at runtime if the asset has no entry.
/// The readable reference is docs/narrative/cast-and-copy.md (Farm Game > Narrative > Write Copy Reference).
/// </summary>
public static class NarrativeDefaults
{
    public const string Mayor = "Mayor Bramble";
    public const string Pippa = "Pippa the Tinker";
    public const string Harry = "Harry the Carpenter";
    public const string Finch = "Old Finch";
    public const string Marta = "Marta";
    public const string Pip = "Pip";
    public const string Hazel = "Hazel";

    public static CastMember[] Cast => new[]
    {
        new CastMember { id = "mayor", displayName = Mayor,
            role = "Mayor of the valley town, and the player's first friend.",
            voice = "Warm, proud, a touch formal. Speaks for the whole town and celebrates milestones, sometimes with a small gift. Opens \"Dear {farmName},\" and signs \"- Mayor Bramble\"." },
        new CastMember { id = "pippa", displayName = Pippa,
            role = "Inventor who builds gadgets for farms (Compost Bay, equipment).",
            voice = "Excitable and quick, loves a clever contraption. Opens \"Hey {farmName}!\" and signs \"- Pippa\"." },
        new CastMember { id = "harry", displayName = Harry,
            role = "Runs the carpenter's shop at the market: tools (axe, fishing pole) and buildings.",
            voice = "Friendly, plainspoken tradesman with a folksy streak (\"Howdy\", \"mighty\"). Practical advice about wood and building. Signs \"- Harry\"." },
        new CastMember { id = "finch", displayName = Finch,
            role = "Retired fisherman who knows every inch of the lake. Also posts Town Requests.",
            voice = "Slow, dry, few words; fond of the lake and of patience. Opens \"Well now,\" or just the farm name. Signs \"- Old Finch\"." },
        new CastMember { id = "marta", displayName = Marta,
            role = "Runs the jam stall; the town's preserves expert. Also posts Town Requests.",
            voice = "Cheerful, chatty and food-obsessed; always angling for a jar. Opens \"Hello {farmName}!\" and signs \"- Marta\"." },
        new CastMember { id = "hazel", displayName = Hazel,
            role = "Runs the seed stall at the Market. Sells every crop's seed packet.",
            voice = "Warm, practical, loves a bargain and a good tip about what grows well. Opens \"Hi {farmName}!\" and signs \"- Hazel\"." },
        new CastMember { id = "pip", displayName = Pip,
            role = "Kid with a picky pet rabbit. Posts Town Requests (no mail yet).",
            voice = "Eager and a little dramatic about the rabbit." },
    };

    public static LetterDef[] Letters => new[]
    {
        new LetterDef { id = "seed_stall_intro", triggerEvent = "run_ended:1", newPlayersOnly = true,
            senderName = Hazel, subject = "Fresh seeds at my stall",
            body = "Hi {farmName}!\n\nHazel here - I run the seed stall at the Market. Saw your radishes come up. Lovely work!\n\nWhen you're ready to try something new, come see me. Carrots are next to nothing, and they sell for nearly twice as much.\n\nOnce you buy a packet, that crop is yours to plant every run after.\n\n- Hazel",
            ctaKind = CtaKind.OpenPlantsShop },

        new LetterDef { id = "regrow_bought", triggerEvent = "seed_bought:regrow", newPlayersOnly = true,
            senderName = Hazel, subject = "These ones keep giving",
            body = "Hi {farmName}!\n\nGood choice! That one's a regrower. Pick it, and as long as the plant survives, it grows back and gives you another harvest - no new seed needed.\n\nKeep it watered and keep the pests off, and it'll pay for itself many times over.\n\n- Hazel" },

        new LetterDef { id = "town_board_intro", triggerEvent = "run_ended:4", newPlayersOnly = true,
            senderName = Mayor, subject = "The town could use a hand",
            body = "Dear {farmName},\n\nWord's getting around about your farm! A few of us in town could use a hand now and then, so we post what we need on the community board by the Market.\n\nHelp out, and folks won't forget it. Every delivery earns you Reputation, and a good name in this valley opens doors - your Barn will show you how.\n\nI've pinned the first request myself.\n\n- Mayor Bramble",
            ctaKind = CtaKind.OpenTownRequests },

        new LetterDef { id = "first_run_done", triggerEvent = "run_ended:1", newPlayersOnly = true,
            senderName = Mayor, subject = "Your first harvest!",
            body = "Dear {farmName},\n\nWhat a sight - crops growing on that old farm again! The whole town is talking.\n\nThose Coins you earned are yours to keep. Spend them on farm upgrades and your next run will go even further. If you're not sure where to start, a bigger field is never a bad idea.\n\n- Mayor Bramble",
            ctaKind = CtaKind.OpenFarmUpgrades },

        new LetterDef { id = "axe_offer", triggerEvent = "run_ended:2", newPlayersOnly = true,
            senderName = Harry, subject = "Those trees won't chop themselves",
            body = "Howdy {farmName},\n\nHarry here, the carpenter down at the market. Noticed the woods behind your place are getting mighty overgrown. That timber's yours for the taking!\n\nCome see me and I'll set you up with an axe. Wood sells at the rack, and a good builder always needs more of it.\n\n- Harry",
            ctaKind = CtaKind.OpenCarpenter },

        new LetterDef { id = "first_tree", triggerEvent = "tree_felled", newPlayersOnly = true,
            senderName = Harry, subject = "Timber!",
            body = "{farmName},\n\nHeard that one fall from here! Nice swing.\n\nYour wood stacks up on the rack by the woods. Sell some for Coins, but hang on to a pile, too. Before long you'll want it for fuel, and I'll have buildings to put up for you.\n\n- Harry" },

        new LetterDef { id = "pole_offer", triggerEvent = "run_ended:3|axe_bought", newPlayersOnly = true,
            senderName = Finch, subject = "The fish are biting",
            body = "Well now, {farmName}.\n\nThat lake past your farm is full of perch, and nobody's cast a line in it for years. Shame, that.\n\nHarry keeps a fishing pole or two in his shop. Get yourself one and come down to the water. Patience is the whole trick of it.\n\n- Old Finch",
            ctaKind = CtaKind.OpenCarpenter },

        new LetterDef { id = "first_fish", triggerEvent = "fish_caught", newPlayersOnly = true,
            senderName = Finch, subject = "Your first catch",
            body = "Ha! Knew you had it in you, {farmName}.\n\nHere's something an old fisherman will tell you for free: watch for the whirlpools. That's where the fish gather, and a line cast there gets a bite much quicker.\n\n- Old Finch" },

        new LetterDef { id = "cannery_unlock", triggerFeatureFlag = "cannery_unlocked",
            senderName = Marta, subject = "Jam season!",
            body = "Hello {farmName}!\n\nMarta here, from the jam stall. Word is you've been learning to preserve, and I couldn't be happier!\n\nHave Harry build you a Cannery. Your crops can go into jars instead of being sold, and a good jar is worth far more than the fruit it came from.\n\nHazel's got strawberry seeds in now, too - they make the quickest jam there is!\n\nSave me a jar of strawberry?\n\n- Marta",
            ctaKind = CtaKind.OpenCarpenter },

        new LetterDef { id = "smokehouse_unlock", triggerFeatureFlag = "smokehouse_unlocked",
            senderName = Finch, subject = "A proper smokehouse",
            body = "{farmName},\n\nSo you've learned the old smoking ways. Good. Raw fish sells, but smoked fish? That's worth several times as much.\n\nHarry can build you a Smokehouse near the lake. Keep the fire fed with wood and the fish will do the rest.\n\n- Old Finch",
            ctaKind = CtaKind.OpenCarpenter },

        new LetterDef { id = "cannery_built", triggerEvent = "built:building_cannery_built",
            senderName = Harry, subject = "Your Cannery is ready",
            body = "{farmName},\n\nAll built, and sturdy as they come! Load your Cannery up with crops, then keep the firebox stocked with wood.\n\nIf the fire goes out, nothing spoils. The jars just wait for you.\n\n- Harry" },

        new LetterDef { id = "smokehouse_built", triggerEvent = "built:building_smokehouse_built",
            senderName = Harry, subject = "Your Smokehouse is ready",
            body = "{farmName},\n\nSmokehouse is up! Old Finch already came by to inspect the chimney.\n\nPut raw fish in the smoker and keep the fire fed with wood. If it burns out, the fish just wait.\n\n- Harry" },

        new LetterDef { id = "first_request", triggerEvent = "town_request_done", newPlayersOnly = true,
            senderName = Mayor, subject = "The town is grateful",
            body = "Dear {farmName},\n\nWord gets around fast in a town this size, and folks are singing your praises! Every request you fill earns you Reputation.\n\nFill the Reputation bar and you'll earn a point to spend at your Barn, making your whole farm better at what it does.\n\n- Mayor Bramble",
            ctaKind = CtaKind.OpenBarn },

        new LetterDef { id = "first_animal", triggerEvent = "animal_unlocked", newPlayersOnly = true,
            senderName = Mayor, subject = "A new friend on the farm",
            body = "Dear {farmName},\n\nI hear you've got some company out there! Animals do more than keep you company - each one changes how your farm runs.\n\nOpen your animals menu to put your new friend to work.\n\n- Mayor Bramble",
            ctaKind = CtaKind.OpenAnimals },

        new LetterDef { id = "extra_packets", triggerEvent = "upgrade:zone_unlock_2", newPlayersOnly = true,
            senderName = Hazel, subject = "One crop, two fields?",
            body = "Hi {farmName}!\n\nA second field - congratulations! You can plant a different crop in each field, or double down on one.\n\nEvery packet you own lets a crop grow in one field. Want corn in both fields to pile up compost, or radishes everywhere to fill a big order? Pick up a second packet at my stall.\n\nMixing crops spreads your luck - deer and crows each have their favorites!\n\n- Hazel",
            ctaKind = CtaKind.OpenPlantsShop },

        new LetterDef { id = "field_two", triggerEvent = "upgrade:zone_unlock_2", newPlayersOnly = true,
            senderName = Mayor, subject = "More room to grow",
            body = "Dear {farmName},\n\nA second field! You're turning that old place into a real farm.\n\nDon't forget to pick seeds for it before your next run - an empty field grows nothing.\n\nAnd a little secret from someone who's watched plenty of farms grow: a helper can only be in one place at a time. If your new field is slow to get going, a second pair of hands works wonders. A quicker helper or a sprinkler helps too - less time hauling water means more time planting!\n\n- Mayor Bramble",
            ctaKind = CtaKind.OpenFieldPicker },

        new LetterDef { id = "long_run_1h", triggerEvent = "run_survived:1h", newPlayersOnly = true,
            senderName = Mayor, subject = "The talk of the valley",
            body = "Dear {farmName},\n\nA whole hour of harvests without a break! Folks in town can hardly believe it.\n\nPlease accept this small token from all of us. Keep it up!\n\n- Mayor Bramble",
            rewardKind = RewardKind.Gems, rewardAmount = 25 },

        new LetterDef { id = "long_run_3h", triggerEvent = "run_survived:3h", newPlayersOnly = true,
            senderName = Mayor, subject = "A farm to be proud of",
            body = "Dear {farmName},\n\nThree whole hours! Travelers are stopping in town just to ask about your farm.\n\nThe council voted, and this gift is from all of us. Well earned.\n\n- Mayor Bramble",
            rewardKind = RewardKind.Gems, rewardAmount = 50 },

        new LetterDef { id = "town_gift", triggerEvent = "welcome_basket_done",
            senderName = Mayor, subject = "A little thank-you",
            body = "Dear {farmName},\n\nThe whole town loved your basket. We'll leave a little something at your farm now and then.\n\n- Mayor Bramble" },
    };

    public static TipDef[] Tips => new[]
    {
        // First session, in order (spotlight steps).
        new TipDef { id = "onboarding_mailbox", when = "First session 1/5: right after naming the farm (spotlights the mailbox)",
            text = "Mail arrives here. Tap to read your welcome letter from the Mayor." },
        new TipDef { id = "onboarding_field", when = "First session 2/5: closing the mailbox after the welcome letter (spotlights Field)",
            text = "The Mayor's seeds are waiting in your shed. Tap here to choose what to plant." },
        new TipDef { id = "onboarding_pick_seeds", when = "First session 3/5: seed picker open (spotlights the whole picker)",
            text = "Tap a seed to plant it in your field, then tap Save." },
        new TipDef { id = "onboarding_start_run", when = "First session 4/5: after saving seeds (spotlights Start Run)",
            text = "Your helpers will plant, water and harvest for you. Tap Start Run and watch your farm grow!" },
        new TipDef { id = "onboarding_money_note", when = "First session 5/5: the first run starts",
            text = "Each harvest earns Money to buy more seeds during this run.\n\nCoins are yours to keep, even after the run ends. Spend them to grow your farm!" },

        // How-to tips, each shown once the first time.
        new TipDef { id = "tip_out_of_money", when = "Any run: first time a helper can't afford a seed bag (points at the seed counter)",
            text = "You're out of Money for seeds! Every harvest earns more, but seed bags cost more the longer a run goes. When you can't buy seeds and nothing is growing, the run ends." },
        new TipDef { id = "tip_farm_upgrades", when = "First time the Farm upgrades menu opens",
            text = "Spend your Coins here to make every run better. A bigger field is a great first buy!" },
        new TipDef { id = "tip_quests", when = "First time the Quests menu opens",
            text = "Finish daily quests to earn Gems. New quests arrive every day." },
        new TipDef { id = "tip_daily_rewards", when = "First time the daily rewards calendar opens",
            text = "Every visit moves you one gift along the week, and each gift is bigger than the last. Collect all 7 for a bonus!" },
        new TipDef { id = "tip_market", when = "First arrival at the Market",
            text = "Welcome to the market! Look around - shops here sell seeds, tools and buildings. Check the notice board for townsfolk who need a hand." },
        new TipDef { id = "tip_woods_no_axe", when = "First arrival at the Woods without an axe",
            text = "These trees are yours to chop, but you'll need an axe first. Harry the Carpenter sells one at the market." },
        new TipDef { id = "tip_woods", when = "First arrival at the Woods with an axe",
            text = "Tap a tree to swing your axe. Each chop knocks loose some wood, and chopped trees grow back over time." },
        new TipDef { id = "tip_wood_rack", when = "First time the Wood Rack opens",
            text = "Your wood piles up here. Sell it for Coins, or save it to fuel your buildings later." },
        new TipDef { id = "tip_lake", when = "First arrival at the Lake with a fishing pole",
            text = "Press and hold to aim your cast, then let go to throw. When a fish bites, tap or hold to reel it in. Whirlpools hold extra fish!" },
        new TipDef { id = "tip_first_catch", when = "First fish caught",
            text = "Nice catch! Fish go to your pantry. Sell them raw, or smoke them in a Smokehouse for much more." },
        new TipDef { id = "tip_smokehouse", when = "First time the Smokehouse opens",
            text = "Load raw fish into the smoker, then keep the fire burning with wood. If the fire goes out, nothing spoils - smoking just pauses." },
        new TipDef { id = "tip_cannery", when = "First time the Cannery opens",
            text = "During a run, some harvested crops go into your jars instead of being sold. Keep the firebox full of wood and they'll become preserves worth far more." },
        new TipDef { id = "tip_compost", when = "First compost earned",
            text = "Your Compost Bay turned a lost crop into compost! Spend compost to speed up research." },
        new TipDef { id = "tip_research", when = "First time Research opens",
            text = "Pick a topic to study. Research finishes over real time, even while you're away, and gives your farm permanent boosts." },
        new TipDef { id = "tip_animals", when = "First time the Animals menu opens",
            text = "Animals change how your farm plays. Unlock them with Gems from quests and daily gifts, then equip one for your runs." },
        new TipDef { id = "tip_town_requests", when = "First time the Town Requests board opens",
            text = "Townsfolk post what they need here. Deliveries earn Reputation, and a full bar gives you a point to spend at your Barn. Switch to Collect during a run to keep harvests for requests." },
        new TipDef { id = "tip_barn", when = "First time the Barn (Farm Level) opens",
            text = "Spend skill points to make your farm better at what it does. Tap a numbered milestone to see what it unlocks." },
        new TipDef { id = "tip_collect_sell", when = "First run after the town-board letter (points at the Collect / Sell switch)",
            text = "Sell turns each harvest into Money right away, to keep your run going. Collect keeps harvests as items, for town requests and your jars." },
        new TipDef { id = "tip_seed_stall", when = "First time Hazel's seed stall opens",
            text = "Buy a seed packet once and that crop is yours to plant every run. The mystery packets are still waiting to be discovered!" },
        new TipDef { id = "tip_extra_packet", when = "Hazel's stall opened once a crop can take a 2nd packet (more fields than packets)",
            text = "Each packet lets a crop grow in one field. Buy another to plant it in more fields at once - great for going all-in on compost, jam or a big town order." },
        new TipDef { id = "tip_idle_field", when = "A run where a field with a crop chosen is still bare after a few minutes",
            text = "This field is still waiting its turn - your helper has its hands full with your other crops! Another helper, a speedier one, or a sprinkler would lend a hand." },
        new TipDef { id = "tip_regrow", when = "First regrowing crop harvested in a run",
            text = "This crop regrows! As long as the plant survives after it's picked, it grows more - no new seed needed." },
        new TipDef { id = "tip_barn_spend", when = "Barn opened with a skill point to spend (points at a + button)",
            text = "You have a point to spend! Tap + to level up a skill. Each level makes your farm a little better at that job." },
        new TipDef { id = "tip_almanac", when = "First time the Farmer's Almanac opens",
            text = "Everything you've discovered is written down here. Tap any page to see its numbers, including how much your upgrades have added." },

        // Farmer's Almanac page blurbs ("why pick this"). Equipment/animal pages fall back to the
        // asset's own description unless an almanac_equipment_<id> / almanac_animal_<id> entry is added.
        new TipDef { id = "almanac_crop_radish", when = "Almanac: Radish page",
            text = "In and out of the ground before pests notice. Tiny payout, lightning fast." },
        new TipDef { id = "almanac_crop_carrot", when = "Almanac: Carrot page",
            text = "Buried where crows can't reach - but deer will dig for them." },
        new TipDef { id = "almanac_crop_corn", when = "Almanac: Corn page",
            text = "Tall stalks shrug off deer, and the harvest waits a while for you. Crows can't resist it." },
        new TipDef { id = "almanac_crop_tomato", when = "Almanac: Tomato page",
            text = "Sturdy vines that keep on giving. Thirsty, though, and deer know where to find them." },
        new TipDef { id = "almanac_crop_strawberry", when = "Almanac: Strawberry page",
            text = "Every bird in the valley knows when these are ripe. Makes the quickest jam at the Cannery." },
        new TipDef { id = "almanac_crop_blueberry", when = "Almanac: Blueberry page",
            text = "Slow to settle in, then fruits again and again. Thirsty, and a crow's favourite - but deer mostly leave it be." },
        new TipDef { id = "almanac_crop_green_beans", when = "Almanac: Green Beans page",
            text = "Quick to crop and quick to grow back. Tender, though, and deer can't get enough of them." },
        new TipDef { id = "almanac_crop_green_pepper", when = "Almanac: Green Pepper page",
            text = "Tough and long-lived. Crows ignore the green fruit, but deer nibble the leaves - plant it where crows are the problem." },
        new TipDef { id = "almanac_crop_red_pepper", when = "Almanac: Red Pepper page",
            text = "The green pepper's twin, left to ripen. Too hot for deer, but crows can't resist the red - plant it where deer are the problem." },
        new TipDef { id = "almanac_pest_deer", when = "Almanac: Deer page",
            text = "Deer wander in from the woods and graze on your fields. They go easy on seeds but hit young plants hardest. Fences keep them out, and a dog will chase them off." },
        new TipDef { id = "almanac_pest_crow", when = "Almanac: Crows page",
            text = "Crows swoop down and peck at your crops - fresh seeds and ripe berries most of all. A scarecrow scares them off." },
        new TipDef { id = "tip_town_gift", when = "Free Gift chest first unlocks (after the Welcome Basket letter is read)",
            text = "A gift every 30 minutes. This one's on us!" },
    };
}
