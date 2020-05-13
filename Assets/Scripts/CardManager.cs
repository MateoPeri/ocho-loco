using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;
using TMPro;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using System.Text.RegularExpressions;
using ExitGames.Client.Photon;

public class CardManager : PunTurnManager, IPunObservable, IPunTurnManagerCallbacks
{
    public static CardManager Instance;

    public List<Sprite> cardSprites = new List<Sprite>();
    public List<Card> allCards;
    public Queue<Card> remainingCards; // cartas del montón (para robar)
    public Stack<Card> playingCardStack; // el montón con el que se juega

    List<Card>[] playerCardList; // una lista de listas para guardar las cartas de los jugadores

    public GameObject playerCardSelectorPrefab, otherPlayerSelectorPrefab;
    public Transform pcsParent;

    public Image stealingStack, playingStack;
    public TMP_Text stealingStackText;

    private Dictionary<int, PlayerCardSelector> playerCardSelectors;

    public int pasarPenal = 3;
    public int vp1Penal = 3;
    public int paloForzado = -1;

    public List<Card> MyCards;

    public int nBarajas = 1;
    public bool isPlayerTurn;

    List<KeyValuePair<Player, object>> turnHistory = new List<KeyValuePair<Player, object>>();

    public bool debug;

    public int playerTurnIndex;

    private void Awake()
    {
        Instance = this;
        // photonView = GetComponent<PhotonView>();
        PhotonPeer.RegisterType(typeof(Card), (byte)'C', Card.SerializeCard, Card.DeserializeCard);
        TurnManagerListener = this;
        TurnDuration = -1f;
    }

    /*
     * TODO Animación de recorrer tus cartas
     * TODO Función que te lleve a una cierta carta (via index o via carta)
     * TODO Cuando intentas robar, si tienes una carta que vale que te la muestre.
     */

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            /*
            GroupCollection[] digits = cardSprites.Select(x => Regex.Match(x.name, @"(\d+)(\D)(\d+)").Groups).ToArray();
            allCards = new List<Card>();
            foreach (GroupCollection value in digits)
            {
                if (int.Parse(value[3].Value) == 0) // para las cartas especiales ej (cards_0_0)
                    continue;
                // Debug.Log(value[1].Value + ", " + value[3].Value); // 0 -> 0_12; 1 -> 0; 2 -> _; 3 -> 12
                allCards.Add(new Card(
                    int.Parse(value[1].Value),
                    int.Parse(value[3].Value)
                    ));
            }
            */
            allCards = new List<Card>();
            foreach (Sprite sprite in cardSprites)
            {
                Card c = GetCardFromSprite(sprite);
                if (c.num == 0) // cartas especiales
                    continue;
                allCards.Add(c);
            }
            nBarajas = Mathf.Max(1, Mathf.CeilToInt(PhotonNetwork.CurrentRoom.PlayerCount / 4)); // 1 baraja por cada 4 jugadores --> 20 cartas libres

            remainingCards = new Queue<Card>(allCards);
            playingCardStack = new Stack<Card>();
            playerCardList = RepartirCartas(nBarajas);
            playingCardStack.Push(remainingCards.Dequeue()); // TODO seguro que es dequeue??

            SyncRoomCards();
            BeginTurn();
        }
    }

    public void LeaveRoom() // mover a otro lado porque aquí no pinta nada
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        //UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    public override void OnLeftRoom()
    {
        //PhotonNetwork.Disconnect();
        //UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        Debug.Log("Left room");
    }

    public List<Card>[] RepartirCartas(int n = 1)
    {
        var rnd = new System.Random();
        remainingCards = new Queue<Card>(remainingCards.ToList().Shuffle());

        List<Card>[] pcl = new List<Card>[PhotonNetwork.CurrentRoom.PlayerCount];

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < pcl.Length; j++)
            {
                if (pcl[j] == null)
                    pcl[j] = new List<Card>();
                pcl[j].Add(remainingCards.Dequeue());
            }
        }
        
        int ind = 0;
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            Hashtable props = new Hashtable
            {
                { OchoLoco.PLAYER_CARDS, pcl[ind].ToArray() }
            };
            player.SetCustomProperties(props);


            ind++;
        }
        return pcl;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            // Debug.Log("downloading player cards due to player update");
            DownloadMyCards();
        }
    }

    // Cada jugador sube las cartas de los montones después de hacer una jugada.
    public void SyncRoomCards()
    {
        Hashtable roomProps = new Hashtable
        {
            { OchoLoco.REMAINING_CARDS, remainingCards.ToArray()    },
            { OchoLoco.PLAYING_CARDS,   playingCardStack.ToArray()  }
        };
        if (debug)
        {
            // Debug.Log("uploading room cards... I am " + PhotonNetwork.LocalPlayer.NickName);
            // Debug.Log("RAW sending this " + playingCardStack.Peek());
            // Debug.Log("Sending this: " + new Stack<Card>(((Card[])roomProps[OchoLoco.PLAYING_CARDS]).Reverse()).Peek()); // funcionaaa
        }
        photonView.RPC("SyncRoomCardsRPC", RpcTarget.All, roomProps);
    }

    [PunRPC]
    public void SyncRoomCardsRPC(Hashtable newCards)
    {
        // Debug.Log("syncing room cards");
        // Debug.Log("Got this: " + new Stack<Card>(((Card[])newCards[OchoLoco.PLAYING_CARDS]).Reverse()).Peek());
        if (newCards.TryGetValue(OchoLoco.REMAINING_CARDS, out object _remainingCards))
        {
            var arr = (Card[])_remainingCards;
            remainingCards = new Queue<Card>(arr);
            // Debug.Log("downloading remaining cards. null: " + (remainingCards == null));
        }

        if (newCards.TryGetValue(OchoLoco.PLAYING_CARDS, out object _playingCards))
        {
            var newStack = new Stack<Card>(((Card[])_playingCards).Reverse());
            // Debug.Log("downloading playing cards. null: " + (newStack == null));
            // if (playingCardStack != null)
            //    Debug.Log(playingCardStack.Peek() + " | " + newStack.Peek());
            /*
            if (playingCardStack.Peek() != newStack.Peek())
            {
                Debug.Log("trying again...");
                UploadRoomCards();
                return;
            }
            */
            playingCardStack = newStack;
        }

        RefreshStacks();
    }

    public void RefreshStacks() // llamado después de cada jugada
    {
        if (remainingCards.Count == 0)
        {
            var toKeep = playingCardStack.Pop(); // La carta que se está jugando
            remainingCards = new Queue<Card>(playingCardStack.Reverse()); // le damos la vuelta (barajamos también?)
            playingCardStack.Clear(); // vaciamos el playing stack
            playingCardStack.Push(toKeep); // y le añadimos solo la carta que se está jugando
        }

        stealingStack.sprite = GetCardSprite("back_0_0");
        stealingStackText.text = remainingCards.Count.ToString();

        playingStack.sprite = GetCardSprite(playingCardStack.Peek());
    }

    public void DownloadMyCards() // Cada jugador llama a esto para obtener sus cartas al comienzo de cada turno.
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(OchoLoco.PLAYER_CARDS, out object _myCards))
        {
            var cs = (Card[])_myCards;
            MyCards = cs.ToList();
            // Debug.Log("downloading my cards. null: " + (MyCards == null));
        }

        RefreshPlayerCardSelectors(); // BUG esto se está llamando antes que los downloads!!!
    }

    // Cada jugador sube sus cartas al final de cada turno. El Master Client las recoge y las guarda en las properties de la room.
    public void UploadMyCards()
    {
        Hashtable props = new Hashtable
        {
            { OchoLoco.PLAYER_CARDS, MyCards.ToArray() }
        };
        // Debug.Log("uploading my cards");
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private void RefreshPlayerCardSelectors()
    {
        //Debug.Log("null check " + (MyCards == null));
        playerCardSelectors = new Dictionary<int, PlayerCardSelector>();
        int c = 0;
        foreach (Transform child in pcsParent)
        {
            Destroy(child.gameObject); // TODO: destruir cada vez que hay que refrescar es muy bestia
        }
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            bool isMe = p.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
            GameObject obj = Instantiate((isMe) ? playerCardSelectorPrefab : otherPlayerSelectorPrefab);
            obj.transform.SetParent(pcsParent);
            obj.transform.localScale = Vector3.one;
            RectTransform rt = obj.GetComponent<RectTransform>();

            if (isMe)
            {
                rt.anchoredPosition = new Vector2(0, -175);
                rt.sizeDelta = new Vector2(450, 100);
            }
            else
            {
                switch (c)
                {
                    case 0:
                        rt.anchoredPosition = new Vector2(350, 0);
                        rt.localRotation = Quaternion.Euler(new Vector3(0, 0, 90));
                        break;
                    case 1:
                        rt.anchoredPosition = new Vector2(-350, 0);
                        rt.localRotation = Quaternion.Euler(new Vector3(0, 0, 270));
                        break;
                    case 2:
                        rt.anchoredPosition = new Vector2(0, 175);
                        rt.localRotation = Quaternion.Euler(new Vector3(0, 0, 180));
                        break;
                    default:
                        break;
                }
                c++;
            }

            var item = obj.GetComponent<PlayerCardSelector>();
            item.Initialize(p, p.ActorNumber, p.NickName, p.IsMasterClient);

            /*
            if (p.CustomProperties.TryGetValue(OchoLoco.PLAYER_READY, out object isPlayerReady))
            {
                item.SetPlayerTurn(isPlayerTurn);
            }
            */
            playerCardSelectors.Add(p.ActorNumber, item);
        }
    }

    public void Paso()
    {
        Debug.Log(PhotonNetwork.LocalPlayer.NickName + " pasa!");
        SendMove(null, true);
    }

    public void VoyPorUna()
    {
        Hashtable props = new Hashtable
        {
            { OchoLoco.PLAYER_VP1, true }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        Debug.Log(PhotonNetwork.LocalPlayer.NickName + " va por 1!");
    }

    public void PlayCard(Card c, Player p, int pForzado = -1)
    {
        if (CanPlayCard(c))// || debug) // TODO debug // si tu carta vale o si tienes un ocho pasas
        {
            playingCardStack.Push(c);
            MyCards.Remove(c); // it should always have it
            if (pForzado == -1)
                paloForzado = pForzado;
            else
                paloForzado = c.palo;
            Debug.Log("Palo forzado = " + pForzado);

            SendMove(c, true);
        }
    }

    public void OnRemainingStackClicked()
    {
        RefreshStacks(); // asegurarse de que el remaining stack se rellena

        if ((!CanPlayAnyCard(true) && remainingCards.Count > 0))// || true) // TODO quitar el debug // solo robas si no puedes jugar ninguna
        {
            Robar(PhotonNetwork.LocalPlayer);
        }
    }

    public void Robar(Player p, bool force=false)
    {
        if (!IsMyTurn(PhotonNetwork.LocalPlayer) && !force)
            return;
        // MyCards.Add(remainingCards.Dequeue());
        // UploadMyCards();

        if (p.CustomProperties.TryGetValue(OchoLoco.PLAYER_CARDS, out object cardL))
        {
            Card c = remainingCards.Dequeue();
            var l = ((Card[])cardL).ToList();
            l.Add(c);
            Hashtable props = new Hashtable
            {
                { OchoLoco.PLAYER_CARDS, l.ToArray() }
            };
            // Debug.Log("uploading my cards");
            p.SetCustomProperties(props);

            // ya no vas por 1
            Hashtable props2 = new Hashtable
            {
                { OchoLoco.PLAYER_VP1, false }
            };
            p.SetCustomProperties(props2);
            //playerCardSelectors[PhotonNetwork.LocalPlayer.ActorNumber].ScrollTo(myCards.Count - 1); // TODO ese index no lo veo yo muy claro
            playerCardSelectors[p.ActorNumber].ScrollToCard(MyCards[MyCards.Count - 1]);
            SyncRoomCards();
        }
    }

    public bool CanPlayAnyCard(bool ignore8s)
    {
        foreach (Card c in MyCards)
        {
            if (ignore8s && c.num == 8)
                continue;
            if (CanPlayCard(c))
                return true;
        }
        return false;
    }

    public bool CanPlayCard(Card c) // palo forzado, 8s, cartas normales
    {
        if (!IsMyTurn(PhotonNetwork.LocalPlayer))
            return false;
        Card currentCard = playingCardStack.Peek();
        if ((c.palo == paloForzado && c.num == currentCard.num) || c.num == 8)
        {
            return true;
        }
        /*
        if (paloForzado != -1) // si hay un palo forzado, tienes q tirar ese palo o un 8
        {
            if (c.palo == paloForzado || c.num == 8)
            {
                return true;
            }
        }
        else
        {
            if (Card.CompareCards(c, currentCard) || c.num == 8) // si tu carta vale o si tienes un ocho pasas
            {
                return true;
            }
        }
        */
        return false;
    }

    public bool IsMyTurn(Player p)
    {
        int myIndex = PhotonNetwork.PlayerList.ToList().IndexOf(p);// + 1;
        return Turn % PhotonNetwork.PlayerList.Length == myIndex;
    }

    public void NoHasPasado(Player sender, Player target)
    {
        Debug.Log(turnHistory.Count);
        if (turnHistory.Count <= Turn - 1) // TODO el primer turno es 0 o 1????
            return;

        Debug.Log("Last card was " + playingCardStack.ElementAt(1).num + ", but " + turnHistory[Turn - 2].Key.NickName + " moved " + turnHistory[Turn - 2].Value.ToString());
        // Si la carta anterior era un 2, y has tirado algo, a robar
        if (playingCardStack.ElementAt(1).num == 2 && turnHistory[Turn - 1].Key == target && turnHistory[Turn - 1].Value != null)
        {
            Debug.Log(sender.NickName + " dice que " + target.NickName + " no ha pasado. A robar.");
            for (int i = 0; i < pasarPenal; i++)
            {
                Robar(target, true);
            }
        }
    }

    public void NoHasDichoVP1(Player sender, Player target)
    {
        if (target.CustomProperties.TryGetValue(OchoLoco.PLAYER_CARDS, out object cardL)
            && target.CustomProperties.TryGetValue(OchoLoco.PLAYER_VP1, out object vp1))
        {
            var l = (Card[])cardL;
            // si tienes 1 carta y no has dicho voy por 1
            if (l.Length == 1 && !(bool)vp1)
            {
                Debug.Log(sender.NickName + " dice que " + target.NickName + " no ha dicho vp1. A robar.");
                for (int i = 0; i < vp1Penal; i++)
                {
                    Robar(target, true);
                }
            }
        }
    }

    public Card GetCardFromSprite(Sprite s)
    {
        GroupCollection digits = Regex.Match(s.name, @"(\d+)(\D)(\d+)").Groups;
        //if (int.Parse(digits[3].Value) == 0) // para las cartas especiales ej (cards_0_0)
        //    return new Card(-1, -1);
        return new Card(int.Parse(digits[1].Value), int.Parse(digits[3].Value));
    }

    public Sprite GetCardSprite(Card c)
    {
        return GetCardSprite(string.Format("cards_{0}_{1}", c.palo, c.num));
    }

    public Sprite GetCardSprite(string sName)
    {
        return cardSprites.Where(x => x.name == sName).FirstOrDefault();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        throw new System.NotImplementedException();
    }

    public void OnTurnBegins(int turn)
    {
        var s = "";
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            playerCardSelectors[p.ActorNumber].ToggleTurnIndicator(IsMyTurn(p));
            if (IsMyTurn(p))
                s = p.NickName;
        }
        Debug.Log("Turn " + turn + " begins (" + s + ")");
        // playerCardSelectors[PhotonNetwork.LocalPlayer.ActorNumber].ToggleTurnIndicator(IsMyTurn()); // no pita
    }

    public void OnTurnCompleted(int turn)
    {
        Debug.Log("Turn " + turn + " complete");
    }

    public void OnPlayerMove(Player player, int turn, object move)
    {
        Debug.Log("Player " + player.NickName + " moved (" + move + ")");
        if (player == PhotonNetwork.LocalPlayer)
        {
            // Debug.Log("Syncying");
            UploadMyCards();
            SyncRoomCards();
        }
    }

    public void OnPlayerFinished(Player player, int turn, object move)
    {
        Debug.Log("Player " + player.NickName + " moved and finished (" + move + ")");
        if (player == PhotonNetwork.LocalPlayer)
        {
            // Debug.Log("Syncying");
            UploadMyCards();
            SyncRoomCards();
        }

        turnHistory.Add(new KeyValuePair<Player, object>(player, move));

        if (player.CustomProperties.TryGetValue(OchoLoco.PLAYER_CARDS, out object cardL))
        {
            var l = (Card[])cardL;
            if (l.Length == 0)
            {
                // Game Finished
                // Add ui
                Debug.Log(player.NickName + " won!!!");
                return;
            }
        }

        BeginTurn(); // new turn
    }

    public void OnTurnTimeEnds(int turn)
    {
        Debug.Log("turn time's up!");
    }
}

[System.Serializable]
public struct Card
{
    public int palo;
    public int num;

    public Card(int palo, int num)
    {
        this.palo = palo;
        this.num = num;
    }

    public static bool CompareCards(Card c1, Card c2)
    {
        return c1.num == c2.num || c1.palo == c2.palo;
    }

    public static bool operator ==(Card c1, Card c2)
    {
        return c1.Equals(c2);
    }

    public static bool operator !=(Card c1, Card c2)
    {
        return !c1.Equals(c2);
    }

    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        var c = (Card)obj;
        return c.palo == palo && c.num == num;
    }

    public override string ToString()
    {
        return string.Format("Card(Palo={0}, Num={1})", palo, num);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public static readonly byte[] memCard = new byte[2 * 4];
    public static short SerializeCard(StreamBuffer outStream, object customobject)
    {
        Card co = (Card)customobject;
        lock (memCard)
        {
            byte[] bytes = memCard;
            int index = 0;
            Protocol.Serialize(co.palo, bytes, ref index);
            Protocol.Serialize(co.num, bytes, ref index);
            outStream.Write(bytes, 0, 2 * 4);
        }

        return 2 * 4;
    }

    public static object DeserializeCard(StreamBuffer inStream, short length)
    {
        Card co = new Card();
        lock (memCard)
        {
            inStream.Read(memCard, 0, 2 * 4);
            int index = 0;
            Protocol.Deserialize(out co.palo, memCard, ref index);
            Protocol.Deserialize(out co.num, memCard, ref index);
        }

        return co;
    }
}
