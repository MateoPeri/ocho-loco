using UnityEngine;
using System.Collections.Generic;

public class OchoLoco
{
    public const int CARD_COUNT = -1;
    public const string REMAINING_CARDS = "RemainingCards";
    public const string PALO_FORZADO = "PaloForzado";
    public const string PLAYING_CARDS = "PlayingCards";
    public const string PLAYER_CARDS = "PlayerCards";
    public const string PLAYER_READY = "IsPlayerReady";
    public const string PLAYER_LOADED_LEVEL = "PlayerLoadedLevel";
    public const string PLAYER_VP1 = "VP1";

    public static Color GetColor(int colorChoice)
    {
        switch (colorChoice)
        {
            case 0: return Color.red;
            case 1: return Color.green;
            case 2: return Color.blue;
            case 3: return Color.yellow;
            case 4: return Color.cyan;
            case 5: return Color.grey;
            case 6: return Color.magenta;
            case 7: return Color.white;
        }

        return Color.black;
    }
}