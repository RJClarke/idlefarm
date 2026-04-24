using System;

[Serializable]
public class ActiveQuest
{
    public string questID;
    public int progress;
    public bool isCompleted;
    public bool isClaimed;
    public string droppedAt; // UTC ISO 8601

    public ActiveQuest() { }

    public ActiveQuest(string questID, string droppedAt)
    {
        this.questID = questID;
        this.droppedAt = droppedAt;
        progress = 0;
        isCompleted = false;
        isClaimed = false;
    }
}
