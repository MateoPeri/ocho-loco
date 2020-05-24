using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using ExitGames.Client.Photon;

public class Console : MonoBehaviourPun //, IPunObservable
{
    #region Logic
    public Stack<string> messageHistory;
    public const string defaultSender = "SYSTEM";

    private void AddToStack(string msg)
    {
        messageHistory.Push(msg);
        Hashtable msgs = new Hashtable
        {
            { OchoLoco.CONSOLE_HISTORY, messageHistory.ToArray() }
        };
        photonView.RPC("SyncMessagesRPC", RpcTarget.Others, msgs);
        HardRefresh();
    }

    [PunRPC]
    public void SyncMessagesRPC(Hashtable msgs)
    {
        if (msgs.TryGetValue(OchoLoco.CONSOLE_HISTORY, out object _msgHistory))
        {
            var newStack = new Stack<string>(((string[])_msgHistory).Reverse());
            messageHistory = newStack;
        }
        HardRefresh(); // TODO esto debería ser refresh
    }

    public void WriteLine(object content, string sender=null)
    {
        if (content == null)
            return;
        AddToStack(Parse(sender == null ? defaultSender : sender, content.ToString()));
    }

    public string Parse(string sender, string message)
    {
        return string.Format("<b><color=\"red\">{0}</color></b>: {1}", sender, message);
    }
    #endregion

    #region MonoBehaviour
    public static Console Instance;
    public string initalMessage = "";
    public int maxMessages = 20;
    public bool displayAll = false;
    public Stack<string> messagesToShow
    {
        get
        {
            if (messageHistory == null)
                return null;
            var max = displayAll ? messageHistory.Count : maxMessages;
            return new Stack<string>(messageHistory.Take(max));
        }
    }
    public RectTransform contentRect;
    public TMP_Text consoleText;

    public void Awake()
    {
        Instance = this;
    }

    // TEST
    /*
     * <b><color="red">SYSTEM</color></b>: Jugador 2234 tira <b><color="blue">2 de picas</color></b>.
     * 
     */

    private void Start()
    {
        Initialize();
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            WriteLine("WriteLine", "Debug");
        }
    }

    public void Initialize()
    {
        messageHistory = new Stack<string>();
        WriteLine("Game Started!"); // Should read: 'SYSTEM: Game Started!'
        HardRefresh();
    }

    public void HardRefresh() // Clears all messages and displays them again
    {
        consoleText.text = initalMessage;
        foreach (var msg in messagesToShow)
        {
            consoleText.text += "\n" + msg.ToString();
        }
        Scroll();
    }

    private void Scroll(float percent=1f) // Scrolls to a percentage of the content
    {
        contentRect.anchoredPosition = new Vector2(0, percent * (contentRect.sizeDelta.y - 100));
    }

    private bool _hidden;
    public void Hide()
    {
        _hidden = !_hidden;
        Debug.Log("hidden: " + _hidden); // TODO: Implement hidden mode (only 1 message visible)
    }

    public void DisplayAll()
    {
        displayAll = !displayAll;
    }
    #endregion
}