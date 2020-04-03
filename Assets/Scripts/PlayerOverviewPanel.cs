using System.Collections.Generic;
using UnityEngine;
using ExitGames.Client.Photon;
using Photon.Realtime;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using TMPro;

public class PlayerOverviewPanel : MonoBehaviourPunCallbacks
{
    public GameObject PlayerOverviewEntryPrefab;
    private Dictionary<int, GameObject> playerListEntries;

    public void Awake()
    {
        playerListEntries = new Dictionary<int, GameObject>();

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            GameObject entry = Instantiate(PlayerOverviewEntryPrefab);
            entry.transform.SetParent(gameObject.transform);
            entry.transform.localScale = Vector3.one;
            entry.GetComponent<TMP_Text>().color = OchoLoco.GetColor(p.GetPlayerNumber());
            entry.GetComponent<TMP_Text>().text = string.Format("{0}\nCartas: {1}", p.NickName, OchoLoco.CARD_COUNT);

            playerListEntries.Add(p.ActorNumber, entry);
        }
    }


    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Destroy(playerListEntries[otherPlayer.ActorNumber].gameObject);
        playerListEntries.Remove(otherPlayer.ActorNumber);
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (playerListEntries.TryGetValue(targetPlayer.ActorNumber, out GameObject entry))
        {
            entry.GetComponent<TMP_Text>().text = string.Format("{0}\nScore: {1}", targetPlayer.NickName, targetPlayer.CustomProperties[OchoLoco.CARD_COUNT]);
        }
    }
}
