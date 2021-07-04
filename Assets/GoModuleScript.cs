using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;
using System.Text.RegularExpressions;

public class GoModuleScript : MonoBehaviour
{

    public KMBombInfo Bomb;
    public KMAudio Audio;

    public KMSelectable[] stoneSelectables;
    public GameObject[] stoneObjects;
    public Material[] stoneMats;
    public int[] stoneData = new int[81]; //0 = none, 1 = black, 2 = white
    static int moduleIdCounter = 1;
    int moduleId;
    private bool allowedToPlace;
    private int stoneNumber;
    private int winner; //1 = black, 2 = white
    private int goesFirst; //1 = black, 2 = white
    private bool TurnDecided;
    private bool StartingPosPlaced;
    private int TurnIndicator; // n%2+1 = 1:black, n%2+1 = 2:white
    private IEnumerator genBoard;
    private int serialThird;
    private int serialSixth;
    private List<int> startPos = new List<int>();
    bool thirdZero = false;
    bool sixthZero = false;
    bool moduleSolved = false;

    void Start()
    {
        moduleId = moduleIdCounter++;
        ClearBoard();
        var serialNumber = Bomb.GetSerialNumber();
        serialThird = (int)char.GetNumericValue(serialNumber[2]);
        serialSixth = (int)char.GetNumericValue(serialNumber[5]);
        if (serialThird == 0)
            thirdZero = true;
        if (serialSixth == 0)
            sixthZero = true;
        if (!thirdZero && !sixthZero)
        {
            startPos.Add((serialSixth - 1) * 9 + serialThird - 1);
            Debug.LogFormat("[Go #{0}] The starting position is column {1}, row {2}. ({1}, {2})", moduleId, serialThird, serialSixth);
        }
        else if (thirdZero && !sixthZero)
        {
            for (int i = 0; i < 9; i++)
            {
                startPos.Add(((serialSixth - 1) * 9) + i);
            }
            Debug.LogFormat("[Go #{0}] The starting position is anywhere in row {1}.", moduleId, serialSixth);
        }
        else if (!thirdZero && sixthZero)
        {
            for (int i = 0; i < 9; i++)
            {
                startPos.Add((serialThird - 1) + (9 * i));
            }
            Debug.LogFormat("[Go #{0}] The starting position is anywhere in column {1}.", moduleId, serialThird);
        }
        else
        {
            for (int i = 0; i < 9; i++)
            {
                startPos.Add(i);
            }
            Debug.LogFormat("[Go #{0}] The starting position is anywhere on the board.", moduleId);
        }
        for (int i = 0; i < stoneSelectables.Length; i++)
        {
            int j = i;
            stoneSelectables[i].OnInteract += delegate ()
            {
                if (allowedToPlace)
                {
                    PlaceStone(j);
                }
                return false;
            };
        }
        GetComponent<KMBombModule>().OnActivate += ActivateModule;
    }

    void ActivateModule()
    {
        WhoWins();
        StartCoroutine(GenerateBoard());
    }

    void WhoWins()
    {
        int batteries = Bomb.GetBatteryCount();
        if (batteries % 2 == 0)
        {
            //black wins (1)
            winner = 1;
            Debug.LogFormat("[Go #{0}] Battery count ({1}) is even.  Black must capture.", moduleId, batteries);
        }
        else
        {
            //white wins (2)
            winner = 2;
            Debug.LogFormat("[Go #{0}] Battery count ({1}) is odd.  White must capture.", moduleId, batteries);
        }
    }

    void Strike()
    {
        GetComponent<KMBombModule>().HandleStrike();
        TurnDecided = false;
        StartCoroutine(ResetBoard());
    }

    void Solve()
    {
        moduleSolved = true;
        for (int i = 0; i < stoneData.Length; i++)
        {
            if (stoneData[i] == 1)
            {
                stoneObjects[i].GetComponent<MeshRenderer>().material = stoneMats[5];
            }
            if (stoneData[i] == 2)
            {
                stoneObjects[i].GetComponent<MeshRenderer>().material = stoneMats[6];
            }
        }
        allowedToPlace = false;
        GetComponent<KMBombModule>().HandlePass();
    }

    void PlaceStone(int placedStone)
    {
        if (!TurnDecided)
        {
            if (stoneObjects[placedStone].activeInHierarchy)
            {
                if (stoneData[placedStone] != goesFirst)
                {
                    Strike();
                    if (stoneData[placedStone] == 1)
                    {
                        Debug.LogFormat("[Go #{0}] Black was chosen to go first, when white should've been chosen.  Strike.", moduleId);
                    }
                    if (stoneData[placedStone] == 2)
                    {
                        Debug.LogFormat("[Go #{0}] White was chosen to go first, when black should've been chosen.  Strike.", moduleId);
                    }
                    for (int i = 0; i < stoneData.Length; i++)
                    {
                        if (stoneData[i] != goesFirst)
                        {
                            var obj = stoneObjects[i];
                            obj.GetComponent<MeshRenderer>().material = stoneData[i] == 1 ? stoneMats[3] : stoneMats[4];
                        }
                    }
                }
                else if (stoneData[placedStone] == 1)
                {
                    TurnIndicator = 0;
                    Debug.LogFormat("[Go #{0}] Black was correctly chosen to go first.", moduleId);
                    StartCoroutine(ShowTurn(TurnIndicator));
                    TurnDecided = true;
                }
                else if (stoneData[placedStone] == 2)
                {
                    TurnIndicator = 1;
                    Debug.LogFormat("[Go #{0}] White was correctly chosen to go first.", moduleId);
                    StartCoroutine(ShowTurn(TurnIndicator));
                    TurnDecided = true;
                }
            }
        }
        else
        {
            if (!stoneObjects[placedStone].activeInHierarchy)
            {
                Audio.PlaySoundAtTransform("goLoud", transform);
                var currentPlayer = TurnIndicator % 2 + 1;
                stoneObjects[placedStone].SetActive(true);
                stoneObjects[placedStone].GetComponent<MeshRenderer>().material = stoneMats[currentPlayer];
                stoneData[placedStone] = currentPlayer;
                TurnIndicator++;

                var captures = FindCaptures();

                if (!StartingPosPlaced)
                {
                    if (placedStone != startPos[0] && !thirdZero && !sixthZero && startPos.Count == 1)
                    {
                        Strike();
                        var obj = stoneObjects[placedStone];
                        obj.GetComponent<MeshRenderer>().material = stoneData[placedStone] == 1 ? stoneMats[3] : stoneMats[4];
                        Debug.LogFormat("[Go #{0}] Starting stone was placed at ({1}, {2}), when it should've been placed at ({3}, {4}). Strike.", moduleId, ((placedStone) % 9) + 1, (placedStone / 9) + 1, serialThird, serialSixth);
                    }
                    else if (!thirdZero && sixthZero)
                    {
                        if (!startPos.Contains(placedStone))
                        {
                            Strike();
                            var obj = stoneObjects[placedStone];
                            obj.GetComponent<MeshRenderer>().material = stoneData[placedStone] == 1 ? stoneMats[3] : stoneMats[4];
                            Debug.LogFormat("[Go #{0}] Starting stone was placed in column {1}, when it should've been placed in column {2}. Strike.", moduleId, placedStone % 9 + 1, serialThird);
                        }
                    }
                    else if (thirdZero && !sixthZero)
                    {
                        if (!startPos.Contains(placedStone))
                        {
                            Strike();
                            var obj = stoneObjects[placedStone];
                            obj.GetComponent<MeshRenderer>().material = stoneData[placedStone] == 1 ? stoneMats[3] : stoneMats[4];
                            Debug.LogFormat("[Go #{0}] Starting stone was placed in row {1}, when it should've been placed in row {2}. Strike.", moduleId, (placedStone / 9) + 1, serialSixth);
                        }
                    }
                    StartingPosPlaced = true;
                }
                Debug.LogFormat("[Go #{0}] Placed a stone at ({1}, {2}).", moduleId, (placedStone % 9) + 1, (placedStone / 9) + 1);
                if (captures.Any())
                {
                    List<int>[] correctCaptures = captures.ToArray();
                    bool selfCapture = false;
                    if (captures.Count == 1 && captures[0].Contains(placedStone))
                    {
                        //self-capture with no other captures.
                        Debug.LogFormat("[Go #{0}] Placed a stone that resulted in a self-capture. Strike.", moduleId);
                        selfCapture = true;
                        Strike();
                    }
                    else // if (captures.Count > 0 && captures.Any(capture => !capture.Contains(placedStone)))
                    {
                        //captures that do not contain self-capture
                        correctCaptures = captures.Where(cap => !cap.Contains(placedStone)).ToArray();
                        if (stoneData[placedStone] == winner)
                        {
                            Solve();
                            if (winner == 1)
                            {
                                Debug.LogFormat("[Go {0}] Black made the capture. Module solved!", moduleId);
                            }
                            else if (winner == 2)
                            {
                                Debug.LogFormat("[Go {0}] White made the capture. Module solved!", moduleId);
                            }
                        }
                        else if (stoneData[placedStone] != winner)
                        {
                            Strike();
                            if (winner == 1)
                            {
                                Debug.LogFormat("[Go #{0}] White made the capture, when black should have captured. Strike.", moduleId);
                            }
                            else if (winner == 2)
                            {
                                Debug.LogFormat("[Go #{0}] Black made the capture, when white should have captured. Strike.", moduleId);
                            }
                        }
                    }
                    foreach (var corrCap in correctCaptures)
                    {
                        foreach (var stone in corrCap)
                        {
                            var obj = stoneObjects[stone];
                            if (stoneData[stone] != winner && !selfCapture)
                            {
                                obj.gameObject.SetActive(false);
                            }
                            else
                            {
                                obj.GetComponent<MeshRenderer>().material = stoneData[stone] == 1 ? stoneMats[3] : stoneMats[4];
                            }
                        }
                    }
                }
            }
            else
            {
                StartCoroutine(ShowTurn(TurnIndicator));
            }
        }
    }

    IEnumerator ShowTurn(int turn)
    {
        int t = turn;
        if (t % 2 == 0)
        {
            for (int i = 0; i < stoneData.Length; i++)
            {
                if (stoneData[i] == 1)
                {
                    var obj = stoneObjects[i];
                    obj.GetComponent<MeshRenderer>().material = stoneMats[7];
                }
            }
        }
        else
        {
            for (int i = 0; i < stoneData.Length; i++)
            {
                if (stoneData[i] == 2)
                {
                    var obj = stoneObjects[i];
                    obj.GetComponent<MeshRenderer>().material = stoneMats[8];
                }
            }
        }
        yield return new WaitForSeconds(1.5f);
        for (int i = 0; i < stoneData.Length; i++)
        {
            if (stoneData[i] == 1)
            {
                var obj = stoneObjects[i];
                obj.GetComponent<MeshRenderer>().material = stoneMats[1];
            }
            if (stoneData[i] == 2)
            {
                var obj = stoneObjects[i];
                obj.GetComponent<MeshRenderer>().material = stoneMats[2];
            }
        }
    }

    void ClearBoard()
    {
        foreach (var stone in stoneObjects)
        {
            stone.gameObject.SetActive(false);
        }
        for (int i = 0; i < stoneData.Length; i++)
        {
            stoneData[i] = 0;
        }
    }

    IEnumerator ResetBoard()
    {
        if (!moduleSolved)
        {
            allowedToPlace = false;
            yield return new WaitForSeconds(1.0f);
            foreach (var stone in stoneObjects)
            {
                if (stone.activeInHierarchy)
                {
                    stone.SetActive(false);
                    yield return new WaitForSeconds(0.05f);
                }
            }
            for (int i = 0; i < stoneData.Length; i++)
            {
                stoneData[i] = 0;
            }
            if (genBoard != null)
            {
                StopCoroutine(genBoard);
            }
            genBoard = GenerateBoard();
            StartCoroutine(genBoard);
        }
    }

    IEnumerator GenerateBoard()
    {
        if (!moduleSolved)
        {
            TryAgain:
            StartingPosPlaced = false;
            ClearBoard();
            List<int> generatedStones = new List<int>();
            generatedStones.Clear();
            int randomStoneCount = Rnd.Range(10, 18);
            for (int i = 0; i < randomStoneCount * 2;)
            {
                int randomStoneNum = Rnd.Range(0, 80);
                if (randomStoneNum == startPos[0] && startPos.Count == 1)
                {
                    //dont place here
                }
                else if (stoneData[randomStoneNum] == 0)
                {
                    if (i % 2 == 0)
                    {
                        stoneData[randomStoneNum] = 1;
                        generatedStones.Add(randomStoneNum);
                    }
                    else
                    {
                        stoneData[randomStoneNum] = 2;
                        generatedStones.Add(randomStoneNum);
                    }
                    i++;
                }
            }
            if (FindCaptures().Any())
            {
                goto TryAgain;
            }
            int blackCount = 0;
            int whiteCount = 0;
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    if (i == 0 || i == 8 || j == 0 || j == 8)
                    {
                        if (stoneData[i * 9 + j] == 1)
                        {
                            blackCount++;
                        }
                        else if (stoneData[i * 9 + j] == 2)
                        {
                            whiteCount++;
                        }
                    }
                }
            }
            if (blackCount > whiteCount)
            {
                goesFirst = 1;
                //black goes first
            }
            else if (whiteCount > blackCount)
            {
                goesFirst = 2;
                //white goes first
            }
            else
            {
                //same border count
                goto TryAgain;
            }
            if (startPos.Count == 1)
            {
                stoneData[startPos[0]] = goesFirst;
            }
            if (FindCaptures().Any())
            {
                goto TryAgain;
            }
            if (startPos.Count == 1)
            {
                stoneData[startPos[0]] = 0;
            }
            LogBoard();
            if (goesFirst == 1)
            {
                Debug.LogFormat("[Go #{0}] The border contains more black pieces than white pieces.  Black goes first.", moduleId);
            }
            else
            {
                Debug.LogFormat("[Go #{0}] The border contains more white pieces than black pieces.  White goes first.", moduleId);
            }

            generatedStones.Sort();
            for (int i = 0; i < generatedStones.Count; i++)
            {
                var stone = generatedStones[i];
                if (stoneData[stone] == 1)
                {
                    stoneObjects[stone].SetActive(true);
                    stoneObjects[stone].GetComponent<MeshRenderer>().material = stoneMats[1];
                    yield return new WaitForSeconds(0.05f);
                }
                if (stoneData[stone] == 2)
                {
                    stoneObjects[stone].SetActive(true);
                    stoneObjects[stone].GetComponent<MeshRenderer>().material = stoneMats[2];
                    yield return new WaitForSeconds(0.05f);
                }
            }
            allowedToPlace = true;
        }
    }

    void LogBoard()
    {
        var svg = new StringBuilder();
        for (int i = 0; i < 9; i++)
        {
            svg.AppendFormat(@"<line x1='{0}' y1='{1}' x2='{2}' y2='{3}' />", 0, i, 8, i);
            svg.AppendFormat(@"<line x1='{0}' y1='{1}' x2='{2}' y2='{3}' />", i, 0, i, 8);
        }
        svg.AppendFormat(@"<circle fill='black' cx='2' cy='2' r='.1' />");
        svg.AppendFormat(@"<circle fill='black' cx='4' cy='4' r='.1' />");
        svg.AppendFormat(@"<circle fill='black' cx='6' cy='6' r='.1' />");
        svg.AppendFormat(@"<circle fill='black' cx='2' cy='6' r='.1' />");
        svg.AppendFormat(@"<circle fill='black' cx='6' cy='2' r='.1' />");
        for (int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 9; j++)
            {
                if (stoneData[i * 9 + j] != 0)
                {
                    svg.AppendFormat(@"<circle fill='{0}' cx='{1}' cy='{2}' r='.4' />", stoneData[i * 9 + j] == 1 ? "black" : "white", j, i);
                }
            }
        }
        Debug.LogFormat(@"[Go #{0}]=svg[Board:]<svg xmlns='http://www.w3.org/2000/svg' viewBox='-.5 -.5 9 9' stroke='black' stroke-width='.05'>{1}</svg>", moduleId, svg.ToString());
    }

    List<List<int>> FindCaptures()
    {
        List<List<int>> Clumps = new List<List<int>>();
        for (int i = 0; i < stoneData.Length; i++)
        {
            if (stoneData[i] != 0)
            {
                var candidateClumps = GetAdjacents(i)
                    .Where(adj => stoneData[i] == stoneData[adj])
                    .Select(adj => Clumps.FirstOrDefault(clump => clump.Contains(adj)))
                    .Where(clump => clump != null)
                    .ToArray();
                var newClump = new List<int>();
                foreach (var clump in candidateClumps)
                {
                    Clumps.Remove(clump);
                    newClump.AddRange(clump);
                }
                newClump.Add(i);
                Clumps.Add(newClump);
            }
        }
        return Clumps
            .Where(clump => clump.All(stone => GetAdjacents(stone).All(adj => stoneData[adj] != 0)))
            .ToList();
    }

    IEnumerable<int> GetAdjacents(int stone)
    {
        if (stone / 9 > 0)
        {
            yield return stone - 9;
        }
        if (stone / 9 < 8)
        {
            yield return stone + 9;
        }
        if (stone % 9 > 0)
        {
            yield return stone - 1;
        }
        if (stone % 9 < 8)
        {
            yield return stone + 1;
        }
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} B4 C5 [click the points in those positions; column first]. Use !{0} tilt ## to see stones obstructed by TP number.";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var btns = new List<KMSelectable>();
        foreach (var piece in command.Split(' '))
        {
            if (piece.Trim().Length == 0)
                continue;
            Match m = Regex.Match(piece, @"^\s*([A-I])([1-9])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!m.Success)
                yield break;
            if (btns.Count == 0)
                yield return null;
            btns.Add(stoneSelectables[m.Groups[1].Value.ToLowerInvariant()[0] - 'a' + 9 * (m.Groups[2].Value[0] - '1')]);
        }
        if (btns.Count > 0)
            yield return btns;
    }
}
