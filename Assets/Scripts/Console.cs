using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using ExitGames.Client.Photon;

public class Console : MonoBehaviourPun
{
    #region Logic
    public Stack<ConsoleMessage> messageHistory;
    public const string defaultSender = "SYSTEM";

    private void AddToStack(ConsoleMessage msg)
    {
        messageHistory.Push(msg);
        Hashtable msgs = new Hashtable
        {
            { OchoLoco.CONSOLE_HISTORY, messageHistory.ToArray() }
        };
        photonView.RPC("SyncRoomCardsRPC", RpcTarget.All, msgs);
    }

    [PunRPC]
    public void SyncMessagesRPC(Hashtable msgs)
    {
        if (msgs.TryGetValue(OchoLoco.CONSOLE_HISTORY, out object _msgHistory))
        {
            var newStack = new Stack<ConsoleMessage>(((ConsoleMessage[])_msgHistory).Reverse());
            messageHistory = newStack;
        }
        HardRefresh(); // TODO esto debería ser refresh
    }

    public void Write(object obj)
    {
        var currentMsg = messageHistory.Pop();
        var newMsg = currentMsg.message.ToString() + obj.ToString();
        currentMsg.message = newMsg;
        AddToStack(currentMsg);
    }

    public void WriteLine(object content, string sender=null)
    {
        AddToStack(new ConsoleMessage(content, sender));
    }

    public void WriteLine(ConsoleMessage msg)
    {
        AddToStack(msg);
    }

    public class ConsoleMessage
    {
        public string sender;
        public object message;

        public ConsoleMessage(object message, string sender=null)
        {
            this.message = message;
            if (sender != null)
                this.sender = sender;
            else
                this.sender = defaultSender;
        }

        public override string ToString()
        {
            return string.Format("<b><color=\"red\">{0}</color></b>: {1}", sender, message);
        }
    }
    #endregion

    #region MonoBehaviour
    public static Console Instance;
    public string initalMessage = "";
    public int maxMessages = 20;
    public bool displayAll = false;
    public Stack<ConsoleMessage> messagesToShow
    {
        get
        {
            if (messageHistory == null)
                return null;
            var max = displayAll ? messageHistory.Count : maxMessages;
            return new Stack<ConsoleMessage>(messageHistory.Take(max));
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
        if (Input.GetKeyDown(KeyCode.S))
        {
            Write("Write");
        }
    }

    public void Initialize()
    {
        messageHistory = new Stack<ConsoleMessage>();
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