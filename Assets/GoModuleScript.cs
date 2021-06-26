using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;

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
    private int TurnIndicator; // n%2+1 = 1:black, n%2+1 = 2:white

    private IEnumerator genBoard;

    void Start()
    {
        moduleId = moduleIdCounter++;
        ClearBoard();
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
        StartCoroutine(GenerateBoard());
        WhoWins();
    }

    void WhoWins()
    {
        int batteries = Bomb.GetBatteryCount();
        if (batteries % 2 == 0)
        {
            //black wins (1)
            winner = 1;
            Debug.LogFormat("[Go #{0}] Battery count ({1}) is even.  Black must win.", moduleId, batteries);
        }
        else
        {
            //white wins (2)
            winner = 2;
            Debug.LogFormat("[Go #{0}] Battery count ({1}) is odd.  White must win.", moduleId, batteries);
        }
    }

    void Strike()
    {
        GetComponent<KMBombModule>().HandleStrike();
        /*/
        for (int i = 0; i < stoneData.Length; i++)
        {
            if (stoneData[i] == 1)
            {
                stoneObjects[i].GetComponent<MeshRenderer>().material = stoneMats[3];
            }
            if (stoneData[i] == 2)
            {
                stoneObjects[i].GetComponent<MeshRenderer>().material = stoneMats[4];
            }
        }
        */
        TurnDecided = false;
        StartCoroutine(ResetBoard());
    }

    void Solve()
    {
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
                    Debug.Log("black has been chosen to place first");
                    TurnDecided = true;
                }
                else if (stoneData[placedStone] == 2)
                {
                    TurnIndicator = 1;
                    Debug.Log("white has been chosen to place first");
                    TurnDecided = true;
                }
            }
            else
            {
                Debug.Log("didn't click stone");
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
                if (captures.Any())
                {
                    List<int>[] correctCaptures = captures.ToArray();
                    bool selfCapture = false;
                    if (captures.Count == 1 && captures[0].Contains(placedStone))
                    {
                        //self-capture with no other captures.
                        Debug.Log("self capture");
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
                            Debug.Log("Correct winner.");
                        }
                        else if (stoneData[placedStone] != winner)
                        {
                            Strike();
                            Debug.Log("Wrong winner.");
                        }
                    }
                    foreach (var corrCap in correctCaptures)
                    {
                        foreach (var stone in corrCap)
                        {
                            //Debug.Log(stone);
                            var obj = stoneObjects[stone];
                            if (stoneData[stone] != winner && !selfCapture)
                            {
                                obj.gameObject.SetActive(false);
                                Debug.Log(stone);
                            }
                            else
                            {
                                obj.GetComponent<MeshRenderer>().material = stoneData[stone] == 1 ? stoneMats[3] : stoneMats[4];
                                Debug.LogFormat("wrong: {0}", stone);
                            }
                        }
                    }
                    Debug.LogFormat("Captures at: \n{0}", captures.Select(capture => capture.Join(", ")).Join("\n"));

                    Debug.Log("Placed at " + placedStone);
                }
                else
                {
                    //Debug.Log("already placed here");
                }
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

    IEnumerator GenerateBoard()
    {
        TryAgain:
        ClearBoard();
        List<int> generatedStones = new List<int>();
        generatedStones.Clear();
        int randomStoneCount = Rnd.Range(10, 18);
        for (int i = 0; i < randomStoneCount * 2;)
        {
            int randomStoneNum = Rnd.Range(0, 80);
            if (stoneData[randomStoneNum] == 0)
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
        else
        {
            WhoGoesFirst();
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



    void LogBoard(int[] StonesLog)
    {
        StringBuilder s = new StringBuilder("");
        int k = 0;
        for (int i = 0; i < 17; i++)
        {
            s.Append("\n");
            for (int j = 0; j < 17; j++)
            {
                if (i == 0)
                {
                    if (j == 0)
                    {
                        if (stoneData[k] == 1)
                            s.Append("B");
                        else if (stoneData[k] == 2)
                            s.Append("W");
                        else
                            s.Append("┌");
                        k++;
                    }
                    else if (j % 2 == 0 && j != 16)
                    {
                        if (stoneData[k] == 1)
                            s.Append("B");
                        else if (stoneData[k] == 2)
                            s.Append("W");
                        else
                            s.Append("┬");
                        k++;
                    }
                    else if (j == 16)
                    {
                        if (stoneData[k] == 1)
                            s.Append("B");
                        else if (stoneData[k] == 2)
                            s.Append("W");
                        else
                            s.Append("┐");
                        k++;
                    }
                    else
                    {
                        s.Append("───");
                    }
                }
                else if (i % 2 == 0 && i != 16)
                {
                    if (j == 0)
                    {
                        if (stoneData[k] == 1)
                            s.Append("B");
                        else if (stoneData[k] == 2)
                            s.Append("W");
                        else
                            s.Append("├");
                        k++;
                    }
                    else if (j == 16)
                    {
                        if (stoneData[k] == 1)
                            s.Append("B");
                        else if (stoneData[k] == 2)
                            s.Append("W");
                        else
                            s.Append("┤");
                        k++;
                    }
                    else if (j % 2 == 0)
                    {
                        if (stoneData[k] == 1)
                            s.Append("B");
                        else if (stoneData[k] == 2)
                            s.Append("W");
                        else
                            s.Append("┼");
                        k++;
                    }
                    else
                    {
                        s.Append("───");
                    }
                }
                else if (i == 16)
                {
                    if (j == 0)
                    {
                        if (stoneData[k] == 1)
                            s.Append("B");
                        else if (stoneData[k] == 2)
                            s.Append("W");
                        else
                            s.Append("└");
                        k++;
                    }
                    else if (j == 16)
                    {
                        if (stoneData[k] == 1)
                            s.Append("B");
                        else if (stoneData[k] == 2)
                            s.Append("W");
                        else
                            s.Append("┘");
                        k++;
                    }
                    else if (j % 2 == 0)
                    {
                        if (stoneData[k] == 1)
                            s.Append("B");
                        else if (stoneData[k] == 2)
                            s.Append("W");
                        else
                            s.Append("┴");
                        k++;
                    }
                    else
                    {
                        s.Append("───");
                    }
                }
                else
                {
                    if (j % 2 == 0)
                    {
                        s.Append("│");
                    }
                    else
                    {
                        s.Append("   ");
                    }
                }
            }
        }
        String str = s.ToString();
        Debug.LogFormat("[Go #{0}] Board:\n{1}", moduleId, str);
    }

    void WhoGoesFirst()
    {
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
            LogBoard(stoneData);
            Debug.LogFormat("[Go #{0}] The border contains more black pieces than white pieces.  Black goes first.", moduleId);
            //Debug.Log("black goes first");
        }
        else if (whiteCount > blackCount)
        {
            goesFirst = 2;
            LogBoard(stoneData);
            Debug.LogFormat("[Go #{0}] The border contains more white pieces than black pieces.  White goes first.", moduleId);
            //Debug.Log("white goes first");
        }
        else
        {
            //Debug.Log("equal");
            foreach (var stone in stoneObjects)
            {
                stone.SetActive(false);
            }
            if (genBoard != null)
            {
                StopCoroutine(genBoard);
            }
            genBoard = GenerateBoard();
            StartCoroutine(genBoard);
        }
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
}
