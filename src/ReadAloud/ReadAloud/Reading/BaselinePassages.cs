namespace ReadAloud.Reading;

/// <summary>Fixed English passages used to baseline listen and synthesis.</summary>
public static class BaselinePassages
{
    public const string FiftyWords =
        "The harbor was quiet enough that the engine note carried across the water. " +
        "A small freighter waited at the outer buoy while the pilot boat turned back toward town. " +
        "Gulls kept to the pilings. " +
        "The mate checked the line once more and looked up at the low cloud. " +
        "Nothing moved.";

    public const string OneHundredFiftyWords =
        "The morning watch started before the harbor lights went out. " +
        "A thin fog hid the far shore from the early watch. " +
        "The freighter held position just outside the marked channel entrance. " +
        "Deckhands coiled the spare line and checked each shackle twice. " +
        "The pilot called over and confirmed the tide was rising. " +
        "Engine orders stayed slow until the bow cleared the buoy. " +
        "Gulls left the pilings when the horn sounded one time. " +
        "The mate marked the time and wrote the heading down. " +
        "Nothing on the radio asked the ship to wait longer. " +
        "Thick clouds kept the harbor light flat and gray. " +
        "The captain kept speed low and watched the markers align. " +
        "A fishing skiff crossed astern and did not change course. " +
        "The crew stayed quiet while the ship settled on track. " +
        "The fog thinned and the town lay dead ahead then. " +
        "The harbor opened and the engine note softened at last.";

    public static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var count = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                inWord = false;
                continue;
            }

            if (!inWord)
            {
                count++;
                inWord = true;
            }
        }

        return count;
    }
}
