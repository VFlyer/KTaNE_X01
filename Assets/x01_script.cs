using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System;
using Random = UnityEngine.Random;
using System.Text.RegularExpressions;

public class x01_script : MonoBehaviour
{

    public KMAudio Audio;
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMSelectable[] Buttons;
    public TextMesh[] SegmentLabelObjects;
    public GameObject[] DartObjects;

    private static int[,] targetScores = new int[5, 3] { { 74, 53, 79 }, { 62, 41, 70 }, { 42, 47, 86 }, { 38, 66, 51 }, { 80, 67, 58 } };
    private List<int> segValues;
    private List<int> doubleValues;
    private List<int> trebleValues;
    private bool[] buttonHasBeenPressed;

    private int TargetScore;
    private int TotalDartsToThrow;
    private string Restrictions;
    private List<string> CorrectSolutions;

    private int PlayerScoreRemaining;
    private int PlayerDartsRemaining;
    private string PlayerDartHistory;

    private static int _moduleIdCounter = 1;
    private int _moduleId = 0;
    private bool isModuleSolved = false;
    private bool isLightsOn = false;

    // Animation Stuff
    private float fadeDuration = 0.6f;
    private float fadeOffset = 0.15f;
    private float[] fadeInStartTimes;
    private bool isShowHappening = false;

    // For autosolver
    private string[] indivDarts;
    private List<int> _pathBtns;

    // Use this for initialization
    void Start()
    {
        _moduleId = _moduleIdCounter++;
        Module.OnActivate += Activate;

        fadeInStartTimes = new float[10];
    }

    // Update is called once per frame
    void Update()
    {
        if (isShowHappening)
        {
            bool allShowsFinished = true;
            for (int showIter = 0; showIter < 10; showIter++)
            {
                if (Time.time < fadeInStartTimes[showIter] + fadeDuration)
                {
                    SegmentLabelObjects[showIter].text = segValues[showIter].ToString();
                    allShowsFinished = false;
                    SegmentLabelObjects[showIter].color = Color.Lerp(Color.clear, Color.black, ((Time.time - fadeInStartTimes[showIter]) / fadeDuration));
                }
            }
            if (allShowsFinished)
            {
                isShowHappening = false;
            }
        }
    }

    void Activate()
    {
        GenerateSolvablePuzzle();
        isLightsOn = true;
    }

    private void Awake()
    {
        for (int i = 0; i < Buttons.Length; i++)
        {
            int j = i;
            Buttons[i].OnInteract += delegate ()
            {
                HandlePress(j);
                return false;
            };
        }
        HideAllPlayerDarts();
    }

    private void HideAllPlayerDarts()
    {
        for (int iter = 0; iter < DartObjects.Length; iter++)
        {
            DartObjects[iter].SetActive(false);
        }
    }
    private void GenerateSolvablePuzzle()
    {
        ResetUsedSegments();

        GenerateRandomBoard();
        ObtainTargetScore();
        ObtainDartCountAndRestrictionSet();

        // For debugging purposes, you can set a specific situation here, like this.
        if (false)
        {
            segValues = new List<int>() { 2, 1, 5, 4, 17, 10, 6, 13, 7, 12 };
            doubleValues = segValues.Select(i => i * 2).ToList();
            trebleValues = segValues.Select(i => i * 3).ToList();

            TargetScore = 71;
            TotalDartsToThrow = 3;
            Restrictions = "CG";
        }

        while (!IsPuzzleSovable())
        {
            GenerateRandomBoard();
            ObtainTargetScore();
            ObtainDartCountAndRestrictionSet();
        }
        PlayerDartsRemaining = TotalDartsToThrow;
        PlayerScoreRemaining = TargetScore;
        PlayerDartHistory = string.Empty;

        DisplaySegmentValues();

        Debug.LogFormat("[X01 #{0}] Generated a solvable board with {1} correct solutions. For example, {2}.", _moduleId, CorrectSolutions.Count, CorrectSolutions[0].ToString());
        Debug.LogFormat("[X01 #{0}] Segment Values, clockwise, starting with North: {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}", _moduleId, segValues[0], segValues[1], segValues[2], segValues[3], segValues[4],
            segValues[5], segValues[6], segValues[7], segValues[8], segValues[9]);
        Debug.LogFormat("[X01 #{0}] Defuser needs to throw {1} darts for exactly {2} points, with restrictions: {3}.", _moduleId, TotalDartsToThrow, TargetScore, Restrictions);
    }
    private void GenerateRandomBoard()
    {
        segValues = new List<int>();
        doubleValues = new List<int>();
        trebleValues = new List<int>();

        while (segValues.Count < 10)
        {
            int randomValue = Random.Range(1, 21);
            if (!segValues.Contains(randomValue))
            {
                segValues.Add(randomValue);
                doubleValues.Add(2 * randomValue);
                trebleValues.Add(3 * randomValue);
            }
        }
    }
    private void ObtainTargetScore()
    {
        List<int> snDigits = KMBombInfoExtensions.GetSerialNumberNumbers(BombInfo).ToList();
        int AAbatteriesPlusSNDigitCount = KMBombInfoExtensions.GetBatteryCount(BombInfo, Battery.AA) + KMBombInfoExtensions.GetBatteryCount(BombInfo, Battery.AAx3) + KMBombInfoExtensions.GetBatteryCount(BombInfo, Battery.AAx4) + snDigits.Count;
        int portsPlusIndicatorsCount = KMBombInfoExtensions.GetPortCount(BombInfo) + KMBombInfoExtensions.GetIndicators(BombInfo).ToList().Count;

        int rowIndex, columnIndex;

        switch (AAbatteriesPlusSNDigitCount)
        {
            case 0: case 1: case 2: rowIndex = 0; break;
            case 3: case 4: rowIndex = 1; break;
            case 5: rowIndex = 2; break;
            case 6: case 7: rowIndex = 3; break;
            default: rowIndex = 4; break;
        }
        switch (portsPlusIndicatorsCount)
        {
            case 0: case 1: case 2: columnIndex = 0; break;
            case 3: case 4: case 5: columnIndex = 1; break;
            default: columnIndex = 2; break;
        }
        TargetScore = targetScores[rowIndex, columnIndex];

        int blackRedSum = 0, greenTanSum = 0;
        for (int iter = 0; iter < segValues.Count; iter++)
        {
            if (iter % 2 == 0)
            {
                blackRedSum += segValues[iter];
            }
            else
            {
                greenTanSum += segValues[iter];
            }
        }
        if (blackRedSum > greenTanSum)
        {
            TargetScore += 10;
        }
        else if (greenTanSum > blackRedSum)
        {
            TargetScore -= 8;
        }
        else
        {
            TargetScore = 69;
        }
    }
    private void ObtainDartCountAndRestrictionSet()
    {
        bool bHasThreeConsecutiveSegValues6AndUnder = false, bHasThreeConsecutiveSegValues15AndOver = false, bHasFourConsecutiveOddSegValues = false,
            bHasThreeConsecutiveEvenSegValues = false, bHasMVGinSerial = false;
        int countOfSegsWithValueHigherThanTen = 0;

        for (int iter = 0; iter < segValues.Count; iter++)
        {
            if (segValues[iter] > 10)
            {
                countOfSegsWithValueHigherThanTen++;
            }
            if (segValues[iter] <= 6 && segValues[(iter + 1) % 10] <= 6 && segValues[(iter + 2) % 10] <= 6)
            {
                bHasThreeConsecutiveSegValues6AndUnder = true;
            }
            if (segValues[iter] >= 15 && segValues[(iter + 1) % 10] >= 15 && segValues[(iter + 2) % 10] >= 15)
            {
                bHasThreeConsecutiveSegValues15AndOver = true;
            }
            if ((segValues[iter] % 2) + (segValues[(iter + 1) % 10] % 2) + (segValues[(iter + 2) % 10] % 2) + (segValues[(iter + 3) % 10] % 2) == 4)
            {
                bHasFourConsecutiveOddSegValues = true;
            }
            if ((segValues[iter] % 2) + (segValues[(iter + 1) % 10] % 2) + (segValues[(iter + 2) % 10] % 2) == 0)
            {
                bHasThreeConsecutiveEvenSegValues = true;
            }
        }
        string bombSerial = KMBombInfoExtensions.GetSerialNumber(BombInfo).ToUpper();
        if (bombSerial.Contains("M") || bombSerial.Contains("V") || bombSerial.Contains("G"))
        {
            bHasMVGinSerial = true;
        }

        if (bHasThreeConsecutiveSegValues6AndUnder)
        {
            TotalDartsToThrow = 3;
            Restrictions = "CG";
        }
        else if (bHasThreeConsecutiveSegValues15AndOver)
        {
            TotalDartsToThrow = 4;
            Restrictions = "DH";
        }
        else if (bHasFourConsecutiveOddSegValues)
        {
            TotalDartsToThrow = 3;
            Restrictions = "AF";
        }
        else if (bHasThreeConsecutiveEvenSegValues)
        {
            TotalDartsToThrow = 4;
            Restrictions = "BD";
        }
        else if (bHasMVGinSerial)
        {
            TotalDartsToThrow = 4;
            Restrictions = "CEI";
        }
        else if (countOfSegsWithValueHigherThanTen == 5)
        {
            TotalDartsToThrow = 3;
            Restrictions = "GH";
        }
        else if (TargetScore <= 45)
        {
            TotalDartsToThrow = 2;
            Restrictions = "";
        }
        else
        {
            TotalDartsToThrow = 3;
            Restrictions = "BEI";
        }
    }
    private void ResetUsedSegments()
    {
        buttonHasBeenPressed = new bool[42];
        for (int iter = 0; iter < 42; iter++)
        {
            buttonHasBeenPressed[iter] = false;
        }
    }
    private void AttemptToClose(int remainingScore, int dartsRemaining, string solutionSoFar)
    {
        bool bTryEveryPossibleCombo = true;
        if (dartsRemaining == 1)
        {
            bTryEveryPossibleCombo = false;
            // Last dart must hit a double
            for (int iter = 0; iter < doubleValues.Count; iter++)
            {
                bool checkThisDouble = true;
                if (Restrictions.Contains("B") && ((iter >= 3 && iter <= 7) || iter == 10))
                {
                    // Not a top-half-of-board double
                    checkThisDouble = false;
                }
                if (Restrictions.Contains("D") && (iter % 2) == 0)
                {
                    // Not a green double segment
                    checkThisDouble = false;
                }
                if (checkThisDouble)
                {
                    if (remainingScore == doubleValues[iter])
                    {
                        bool thisSolutionIsValid = true;

                        string candidate = solutionSoFar + "D" + (iter == 10 ? "B" : segValues[iter].ToString());
                        string[] individualDarts = candidate.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        // Potential Solution Found... Let's check restrictions...

                        if (thisSolutionIsValid && Restrictions.Contains("A"))
                        {
                            for (int candidateIter = 0; candidateIter < individualDarts.Length && thisSolutionIsValid; candidateIter++)
                            {
                                if (individualDarts[candidateIter].StartsWith("S"))
                                {
                                    int segValue = -1;
                                    if (int.TryParse(individualDarts[candidateIter].Substring(1), out segValue))
                                    {
                                        if ((segValue % 2) == 1)
                                        {
                                            thisSolutionIsValid = false;
                                        }
                                    }
                                }
                            }
                        }
                        if (thisSolutionIsValid && Restrictions.Contains("C"))
                        {
                            bool bFoundBottomHalfOfBoardDouble = false;
                            for (int candidateIter = 0; candidateIter < individualDarts.Length && !bFoundBottomHalfOfBoardDouble; candidateIter++)
                            {
                                if (individualDarts[candidateIter].StartsWith("D"))
                                {
                                    int segValue = -1;
                                    if (int.TryParse(individualDarts[candidateIter].Substring(1), out segValue))
                                    {
                                        int indexOfSegValue = segValues.IndexOf(segValue);
                                        if (indexOfSegValue >= 3 && indexOfSegValue <= 7)
                                        {
                                            bFoundBottomHalfOfBoardDouble = true;
                                        }
                                    }
                                }
                            }
                            if (!bFoundBottomHalfOfBoardDouble)
                            {
                                thisSolutionIsValid = false;
                            }
                        }
                        if (thisSolutionIsValid && Restrictions.Contains("H"))
                        {
                            bool bFoundEvenSegmentTreble = false;
                            for (int candidateIter = 0; candidateIter < individualDarts.Length && !bFoundEvenSegmentTreble; candidateIter++)
                            {
                                if (individualDarts[candidateIter].StartsWith("T"))
                                {
                                    int segValue = -1;
                                    if (int.TryParse(individualDarts[candidateIter].Substring(1), out segValue))
                                    {
                                        if ((segValue % 2) == 0)
                                        {
                                            bFoundEvenSegmentTreble = true;
                                        }
                                    }
                                }
                            }
                            if (!bFoundEvenSegmentTreble)
                            {
                                thisSolutionIsValid = false;
                            }
                        }

                        if (thisSolutionIsValid && Restrictions.Contains("I"))
                        {
                            Dictionary<int, int> pointsScored = new Dictionary<int, int>();
                            for (int candidateIter = 0; candidateIter < individualDarts.Length; candidateIter++)
                            {
                                int thisDartScore = GetDartScore(individualDarts[candidateIter]);
                                if (pointsScored.ContainsKey(thisDartScore))
                                {
                                    thisSolutionIsValid = false;
                                }
                                else
                                {
                                    pointsScored.Add(thisDartScore, thisDartScore);
                                }
                            }
                        }
                        else
                        {
                            // Check that no segment is used more than once. Restriction I will cover this already, but if the player doesn't
                            // have that restriction, we need to check here. Singles can be used twice, but everything else cannot be used more than once.
                            bool[] candidatePressed = new bool[42];
                            for (int candidateIter = 0; candidateIter < individualDarts.Length; candidateIter++)
                            {
                                if (individualDarts[candidateIter] == "SB")
                                {
                                    if (candidatePressed[40])
                                    {
                                        thisSolutionIsValid = false;
                                    }
                                    else
                                    {
                                        candidatePressed[40] = true;
                                    }
                                }
                                else if (individualDarts[candidateIter] == "DB")
                                {
                                    if (candidatePressed[41])
                                    {
                                        thisSolutionIsValid = false;
                                    }
                                    else
                                    {
                                        candidatePressed[41] = true;
                                    }
                                }
                                else
                                {
                                    int segVal = -1;
                                    if (int.TryParse(individualDarts[candidateIter].Substring(1), out segVal))
                                    {
                                        int segValIndex = GetSegIndexForValue(segVal);
                                        if (individualDarts[candidateIter].StartsWith("S"))
                                        {
                                            if (candidatePressed[segValIndex])
                                            {
                                                if (candidatePressed[10 + segValIndex])
                                                {
                                                    thisSolutionIsValid = false;
                                                }
                                                else
                                                {
                                                    candidatePressed[10 + segValIndex] = true;
                                                }
                                            }
                                            else
                                            {
                                                candidatePressed[segValIndex] = true;
                                            }
                                        }
                                        else if (individualDarts[candidateIter].StartsWith("D"))
                                        {
                                            if (candidatePressed[20 + segValIndex])
                                            {
                                                thisSolutionIsValid = false;
                                            }
                                            else
                                            {
                                                candidatePressed[20 + segValIndex] = true;
                                            }
                                        }
                                        else if (individualDarts[candidateIter].StartsWith("T"))
                                        {
                                            if (candidatePressed[30 + segValIndex])
                                            {
                                                thisSolutionIsValid = false;
                                            }
                                            else
                                            {
                                                candidatePressed[30 + segValIndex] = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (thisSolutionIsValid)
                        {
                            CorrectSolutions.Add(candidate);
                        }
                    }
                }
            }
        }
        else
        {
            if (Restrictions.Contains("E") && (dartsRemaining == TotalDartsToThrow))
            {
                if (remainingScore - 25 < 0)
                {
                    return;
                }
                else
                {
                    bTryEveryPossibleCombo = false;
                    AttemptToClose(remainingScore - 25, dartsRemaining - 1, "SB ");
                }
            }

            if (Restrictions.Contains("F") && (dartsRemaining == TotalDartsToThrow))
            {
                bTryEveryPossibleCombo = false;
                // Use one dart to hit the required treble
                for (int iter = 0; iter < trebleValues.Count; iter++)
                {
                    // No point in checking if this treble busts us.
                    if (trebleValues[iter] < remainingScore)
                    {
                        AttemptToClose(remainingScore - trebleValues[iter], dartsRemaining - 1, "T" + segValues[iter] + " ");
                    }
                }
            }
            if (Restrictions.Contains("G"))
            {
                if (dartsRemaining == TotalDartsToThrow)
                {
                    bTryEveryPossibleCombo = false;
                    // Use one dart to hit a treble
                    for (int iter = 0; iter < trebleValues.Count; iter++)
                    {
                        // No point in checking if this treble busts us.
                        if (trebleValues[iter] < remainingScore)
                        {
                            AttemptToClose(remainingScore - trebleValues[iter], dartsRemaining - 1, "T" + segValues[iter] + " ");
                        }
                    }
                }
                else if (dartsRemaining == TotalDartsToThrow - 1)
                {
                    bTryEveryPossibleCombo = false;
                    // Use one dart to hit a single
                    for (int iter = 0; iter < segValues.Count; iter++)
                    {
                        // No point in checking if this treble busts us.
                        if (segValues[iter] < remainingScore)
                        {
                            string segNotation = (iter == 10 ? "B" : segValues[iter].ToString());
                            AttemptToClose(remainingScore - segValues[iter], dartsRemaining - 1, solutionSoFar + "S" + segNotation + " ");
                        }
                    }
                }
            }
            if (Restrictions.Contains("H") && (dartsRemaining == TotalDartsToThrow))
            {
                bTryEveryPossibleCombo = false;
                // Use one dart to hit the required even-segment treble
                for (int iter = 0; iter < trebleValues.Count; iter++)
                {
                    if ((trebleValues[iter] % 2) == 0)
                    {
                        // No point in checking if this treble busts us.
                        if (trebleValues[iter] < remainingScore)
                        {
                            AttemptToClose(remainingScore - trebleValues[iter], dartsRemaining - 1, "T" + segValues[iter] + " ");
                        }
                    }
                }
            }

            if (bTryEveryPossibleCombo)
            {
                for (int singlesIter = 0; singlesIter < segValues.Count; singlesIter++)
                {
                    AttemptToClose(remainingScore - segValues[singlesIter], dartsRemaining - 1, solutionSoFar + "S" + (singlesIter == 10 ? "B" : segValues[singlesIter].ToString()) + " ");
                }
                if (dartsRemaining != 2 || ((remainingScore % 2) != 1))
                {
                    for (int doublesIter = 0; doublesIter < doubleValues.Count; doublesIter++)
                    {
                        AttemptToClose(remainingScore - doubleValues[doublesIter], dartsRemaining - 1, solutionSoFar + "D" + (doublesIter == 10 ? "B" : segValues[doublesIter].ToString()) + " ");
                    }
                }
                for (int treblesIter = 0; treblesIter < trebleValues.Count; treblesIter++)
                {
                    AttemptToClose(remainingScore - trebleValues[treblesIter], dartsRemaining - 1, solutionSoFar + "T" + (treblesIter == 10 ? "B" : segValues[treblesIter].ToString()) + " ");
                }
            }
        }
    }
    private int GetDartScore(string dart)
    {
        char chMultiplier = dart[0];
        string segment = dart.Substring(1);
        int segVal = (segment == "B" ? 25 : int.Parse(segment));
        int mult = 1;
        if (chMultiplier == 'D') mult = 2; else if (chMultiplier == 'T') mult = 3;
        return mult * segVal;
    }
    private bool IsPuzzleSovable()
    {
        // Before attempting to solve, put the bullseyes in the singles and doubles values lists
        segValues.Add(25);
        doubleValues.Add(50);
        CorrectSolutions = new List<string>();
        AttemptToClose(TargetScore, TotalDartsToThrow, string.Empty);
        return (CorrectSolutions.Count > 0);
    }
    private void DisplaySegmentValues()
    {
        for (int iter = 0; iter < SegmentLabelObjects.Length; iter++)
        {
            fadeInStartTimes[iter] = Time.time + iter * fadeOffset;
        }
        isShowHappening = true;
    }

    private void HandlePress(int buttonIndex)
    {
        Buttons[buttonIndex].AddInteractionPunch();

        if (isModuleSolved || !isLightsOn)
        {
            return;
        }

        int ringNumber = buttonIndex / 10;
        int segNumber = buttonIndex % 10;
        int pressedValue = 0;
        string segDesc = string.Empty;
        string thisDartHistory = string.Empty;
        switch (ringNumber)
        {
            case 0:
                pressedValue = segValues[segNumber];
                segDesc = "Outer Single " + segValues[segNumber].ToString();
                thisDartHistory = "S" + segValues[segNumber] + " ";
                break;
            case 1:
                pressedValue = segValues[segNumber];
                segDesc = "Inner Single " + segValues[segNumber].ToString();
                thisDartHistory = "S" + segValues[segNumber] + " ";
                break;
            case 2:
                pressedValue = 2 * segValues[segNumber];
                segDesc = "Double " + segValues[segNumber].ToString();
                thisDartHistory = "D" + segValues[segNumber] + " ";
                break;
            case 3:
                pressedValue = 3 * segValues[segNumber];
                segDesc = "Treble " + segValues[segNumber].ToString();
                thisDartHistory = "T" + segValues[segNumber] + " ";
                break;
            case 4:
                pressedValue = 25 * (segNumber + 1);
                segDesc = (segNumber == 1 ? "Double " : "Single ") + "Bull";
                thisDartHistory = (segNumber == 1 ? "D" : "S") + "B ";
                break;
            default:
                segDesc = "something weird. Unhandled!";
                break;
        }
        Debug.LogFormat("[X01 #{0}] Defuser pressed {1}, valued at {2}.", _moduleId, segDesc, pressedValue);
        if (buttonHasBeenPressed[buttonIndex])
        {
            Debug.LogFormat("[X01 #{0}] This segment has been used already. Strike assessed. Resetting module.", _moduleId);
            _pathBtns = new List<int>();
            Module.HandleStrike();
            HideAllPlayerDarts();
            GenerateSolvablePuzzle();
            return;
        }
        // Record the button as used in this solve attempt.
        buttonHasBeenPressed[buttonIndex] = true;
        PlayerDartsRemaining--;
        PlayerDartHistory += thisDartHistory;

        #region Restriction Checks!
        bool restrictionViolationFound = false;
        // Check If Restrictions Followed
        if (Restrictions.Contains("A"))
        {
            if (buttonIndex < 20 && ((pressedValue % 2) == 1))
            {
                Debug.LogFormat("[X01 #{0}] Used a single area of an odd-value segment (Restriction A).", _moduleId);
                restrictionViolationFound = true;
            }
        }
        if (Restrictions.Contains("B") && !restrictionViolationFound)
        {
            if (PlayerDartsRemaining == 0)
            {
                // This is the last dart. Must be a double on the top half of board.
                if (buttonIndex != 20 && buttonIndex != 21 && buttonIndex != 22 && buttonIndex != 28 && buttonIndex != 29)
                {
                    Debug.LogFormat("[X01 #{0}] Last dart not used on a double on the top half of the board (Restriction B).", _moduleId);
                    restrictionViolationFound = true;
                }
            }
        }
        if (Restrictions.Contains("C") && !restrictionViolationFound)
        {
            if (PlayerDartsRemaining == 0)
            {
                bool usedABottomHalfOfBoardDouble = false;
                for (int iter = 23; iter <= 27; iter++)
                {
                    if (buttonHasBeenPressed[iter])
                    {
                        usedABottomHalfOfBoardDouble = true;
                    }
                }
                if (!usedABottomHalfOfBoardDouble)
                {
                    Debug.LogFormat("[X01 #{0}] No bottom-half-of-board double used (Restriction C).", _moduleId);
                    restrictionViolationFound = true;
                }
            }
        }
        if (Restrictions.Contains("D") && !restrictionViolationFound)
        {
            if (PlayerDartsRemaining == 0)
            {
                bool usedAGreenDoubleToClose = false;
                for (int iter = 21; iter <= 29; iter += 2)
                {
                    if (buttonIndex == iter)
                    {
                        usedAGreenDoubleToClose = true;
                    }
                }
                if (!usedAGreenDoubleToClose)
                {
                    Debug.LogFormat("[X01 #{0}] Did not close out using a green double (Restriction D).", _moduleId);
                    restrictionViolationFound = true;
                }
            }
        }
        if (Restrictions.Contains("E") && !restrictionViolationFound)
        {
            if (PlayerDartsRemaining == 0)
            {
                if (!buttonHasBeenPressed[40])
                {
                    Debug.LogFormat("[X01 #{0}] Single bullseye not used (Restriction E).", _moduleId);
                    restrictionViolationFound = true;
                }
            }
        }
        if (Restrictions.Contains("F") && !restrictionViolationFound)
        {
            if (PlayerDartsRemaining == 0)
            {
                bool usedATreble = false;
                for (int iter = 30; iter < 40; iter++)
                {
                    if (buttonHasBeenPressed[iter])
                    {
                        usedATreble = true;
                    }
                }
                if (!usedATreble)
                {
                    Debug.LogFormat("[X01 #{0}] Did not use a treble (Restriction F).", _moduleId);
                    restrictionViolationFound = true;
                }
            }
        }
        if (Restrictions.Contains("G") && !restrictionViolationFound)
        {
            if (PlayerDartsRemaining == 0)
            {
                bool usedASingle = false, usedADouble = false, usedATreble = false;
                for (int iter = 0; iter <= 41; iter++)
                {
                    if (buttonHasBeenPressed[iter])
                    {
                        if (iter < 20)
                            usedASingle = true;
                        else if (iter < 30)
                            usedADouble = true;
                        else if (iter < 40)
                            usedATreble = true;
                        else if (iter == 40)
                            usedASingle = true;
                        else if (iter == 41)
                            usedADouble = true;
                    }
                }
                if (!usedASingle || !usedADouble || !usedATreble)
                {
                    Debug.LogFormat("[X01 #{0}] Did not use a single, double, and treble (Restriction G).", _moduleId);
                    restrictionViolationFound = true;
                }
            }
        }
        if (Restrictions.Contains("H") && !restrictionViolationFound)
        {
            if (PlayerDartsRemaining == 0)
            {
                bool usedAnEvenValuedTreble = false;
                for (int iter = 30; iter < 40; iter++)
                {
                    if (buttonHasBeenPressed[iter])
                    {
                        int usedSegValue = segValues[(iter % 10)];
                        if ((usedSegValue % 2) == 0)
                        {
                            usedAnEvenValuedTreble = true;
                        }
                    }
                }
                if (!usedAnEvenValuedTreble)
                {
                    Debug.LogFormat("[X01 #{0}] Did not use an even-valued treble (Restriction H).", _moduleId);
                    restrictionViolationFound = true;
                }
            }
        }
        if (Restrictions.Contains("I") && !restrictionViolationFound)
        {
            string[] individualPlayerDarts = PlayerDartHistory.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int iter = 0; iter < individualPlayerDarts.Length - 1; iter++)
            {
                int prevDartValue = GetDartScore(individualPlayerDarts[iter]);
                if (prevDartValue == pressedValue)
                {
                    Debug.LogFormat("[X01 #{0}] Dart Value of {1} was already scored on Dart #{2} (Restriction I).", _moduleId, pressedValue, (iter + 1));
                    restrictionViolationFound = true;
                    break;
                }
            }
        }

        if (restrictionViolationFound)
        {
            Debug.LogFormat("[X01 #{0}] Strike assessed. Resetting module.", _moduleId);
            Module.HandleStrike();
            HideAllPlayerDarts();
            GenerateSolvablePuzzle();
            return;
        }
        #endregion
        // Congratulations! You passed all restrictions! But can you still close out? Let's find out.
        // Score the thrown dart
        PlayerScoreRemaining -= pressedValue;

        Debug.LogFormat("[X01 #{0}] Player Now has {1} points and {2} dart(s) remaining.", _moduleId, PlayerScoreRemaining, PlayerDartsRemaining);
        if (PlayerScoreRemaining == 0 && PlayerDartsRemaining == 0)
        {
            // Module Solved!!!
            Audio.PlaySoundAtTransform("disarmed", Module.transform);
            RenderPlayerDart(TotalDartsToThrow - 1, buttonIndex);
            Module.HandlePass();
            isModuleSolved = true;
            Debug.LogFormat("[X01 #{0}] Module Solved with Solution: {1}", _moduleId, PlayerDartHistory);
            return;
        }

        // For autosolver
        if (PlayerHasPathToSolution(PlayerScoreRemaining, PlayerDartsRemaining, PlayerDartHistory))
        {
            var p = new List<string>();
            var c = CorrectSolutions[0].Split(' ');
            for (int i = TotalDartsToThrow - PlayerDartsRemaining; i < c.Length; i++)
                p.Add(c[i]);
            var p2 = p.ToArray();
            _pathBtns = new List<int>();
            _pathBtns = GetPathFromStrings(p2);
        }

        if (!PlayerHasPathToSolution(PlayerScoreRemaining, PlayerDartsRemaining, PlayerDartHistory))
        {
            Debug.LogFormat("[X01 #{0}] However, there is no way to close {1} points with {2} dart(s), while following all restrictions, at this point. Strike assessed, resetting module.", _moduleId, PlayerScoreRemaining, PlayerDartsRemaining);
            Module.HandleStrike();
            HideAllPlayerDarts();
            GenerateSolvablePuzzle();
            return;
        }

        CorrectSolutions = new List<string>();
        AttemptToClose(TargetScore, TotalDartsToThrow, string.Empty);
        // Play dart sound
        RenderPlayerDart(TotalDartsToThrow - PlayerDartsRemaining - 1, buttonIndex);
        Audio.PlaySoundAtTransform("gooddart", Module.transform);

    }

    private bool PlayerHasPathToSolution(int remainingScore, int dartsRemaining, string solutionSoFar)
    {
        if (remainingScore < 2 || dartsRemaining < 1)
            return false;
        if (dartsRemaining == 1)
        {
            bool foundPath = false;
            // Last dart must hit a double
            for (int iter = 0; iter < doubleValues.Count && !foundPath; iter++)
            {
                bool checkThisDouble = true;
                if (Restrictions.Contains("B") && ((iter >= 3 && iter <= 7) || iter == 10))
                {
                    // Not a top-half-of-board double
                    checkThisDouble = false;
                }
                if (Restrictions.Contains("D") && (iter % 2) == 0)
                {
                    // Not a green double segment
                    checkThisDouble = false;
                }
                if (checkThisDouble)
                {
                    if (remainingScore == doubleValues[iter])
                    {
                        bool thisSolutionIsValid = true;
                        string candidate = solutionSoFar + "D" + (iter == 10 ? "B" : segValues[iter].ToString());
                        string[] individualDarts = candidate.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        indivDarts = new string[individualDarts.Length];
                        for (int i = 0; i < indivDarts.Length; i++)
                            indivDarts[i] = individualDarts[i];

                        if (Restrictions.Contains("C"))
                        {
                            bool bFoundBottomHalfOfBoardDouble = false;
                            for (int candidateIter = 0; candidateIter < individualDarts.Length && !bFoundBottomHalfOfBoardDouble; candidateIter++)
                            {
                                if (individualDarts[candidateIter].StartsWith("D"))
                                {
                                    int segValue = -1;
                                    if (int.TryParse(individualDarts[candidateIter].Substring(1), out segValue))
                                    {
                                        int indexOfSegValue = segValues.IndexOf(segValue);
                                        if (indexOfSegValue >= 3 && indexOfSegValue <= 7)
                                        {
                                            bFoundBottomHalfOfBoardDouble = true;
                                        }
                                    }
                                }
                            }
                            if (!bFoundBottomHalfOfBoardDouble)
                            {
                                thisSolutionIsValid = false;
                            }
                        }
                        if (thisSolutionIsValid && Restrictions.Contains("H"))
                        {
                            bool bFoundEvenSegmentTreble = false;
                            for (int candidateIter = 0; candidateIter < individualDarts.Length && !bFoundEvenSegmentTreble; candidateIter++)
                            {
                                if (individualDarts[candidateIter].StartsWith("T"))
                                {
                                    int segValue = -1;
                                    if (int.TryParse(individualDarts[candidateIter].Substring(1), out segValue))
                                    {
                                        if ((segValue % 2) == 0)
                                        {
                                            bFoundEvenSegmentTreble = true;
                                        }
                                    }
                                }
                            }
                            if (!bFoundEvenSegmentTreble)
                            {
                                thisSolutionIsValid = false;
                            }
                        }

                        if (thisSolutionIsValid && Restrictions.Contains("I"))
                        {
                            Dictionary<int, int> pointsScored = new Dictionary<int, int>();
                            for (int candidateIter = 0; candidateIter < individualDarts.Length; candidateIter++)
                            {
                                int thisDartScore = GetDartScore(individualDarts[candidateIter]);
                                if (pointsScored.ContainsKey(thisDartScore))
                                {
                                    pointsScored.Add(thisDartScore, thisDartScore);
                                    thisSolutionIsValid = false;
                                }
                            }
                        }

                        if (thisSolutionIsValid)
                        {
                            foundPath = true;
                        }
                    }
                }
            }
            return foundPath;
        }
        else
        {
            if (Restrictions.Contains("E") && (!solutionSoFar.Contains("SB")))
            {
                string solSoFarWithRequiredBullseye = solutionSoFar + " SB ";
                int pointsAfterRequiredBullseye = remainingScore - 25;
                return PlayerHasPathToSolution(pointsAfterRequiredBullseye, dartsRemaining - 1, solSoFarWithRequiredBullseye);
            }

            if (Restrictions.Contains("F") && (!solutionSoFar.Contains("T")))
            {
                bool bSolFound = false;
                // Use one dart to hit a required treble
                for (int iter = 0; iter < trebleValues.Count && !bSolFound; iter++)
                {
                    // No point in checking if this treble busts us.
                    if (trebleValues[iter] < remainingScore)
                    {
                        bSolFound = bSolFound || PlayerHasPathToSolution(remainingScore - trebleValues[iter], dartsRemaining - 1, solutionSoFar + "T" + segValues[iter] + " ");
                    }
                }
                return bSolFound;
            }
            if (Restrictions.Contains("G"))
            {
                // Thankfully, Restriction G is always with 3 darts. So you either have to do Treble-Single-Double or Single-Treble-Double... We don't have
                // to check for a Double in the list because it'll be the last dart. Checked in the "dartsReamining == 1" above.
                // If we get to this part of the code and there's a Double in the history, they dun goofed up.
                if (solutionSoFar.Contains("D"))
                    return false;
                if (!solutionSoFar.Contains("T"))
                {
                    bool bSolFound = false;
                    // Use one dart to hit a treble
                    for (int iter = 0; iter < trebleValues.Count && !bSolFound; iter++)
                    {
                        // No point in checking if this treble busts us.
                        if (trebleValues[iter] < remainingScore)
                        {
                            bSolFound = bSolFound || PlayerHasPathToSolution(remainingScore - trebleValues[iter], dartsRemaining - 1, solutionSoFar + "T" + segValues[iter] + " ");
                        }
                    }
                    return bSolFound;
                }
                else if (!solutionSoFar.Contains("S"))
                {
                    bool bSolFound = false;
                    // Use one dart to hit a single
                    for (int iter = 0; iter < segValues.Count && !bSolFound; iter++)
                    {
                        // No point in checking if this treble busts us.
                        if (segValues[iter] < remainingScore)
                        {
                            bSolFound = bSolFound || PlayerHasPathToSolution(remainingScore - segValues[iter], dartsRemaining - 1, solutionSoFar + "S" + segValues[iter] + " ");
                        }
                    }
                    return bSolFound;
                }
            }
            if (Restrictions.Contains("H"))
            {
                bool bFoundEvenTreble = false;
                if (solutionSoFar.Contains("T"))
                {

                    string[] individualDarts = solutionSoFar.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    for (int iter = 0; iter < individualDarts.Length; iter++)
                    {
                        if (individualDarts[iter].StartsWith("T"))
                        {
                            int tVal = int.Parse(individualDarts[iter].Substring(1));
                            if ((tVal % 2) == 0)
                                bFoundEvenTreble = true;
                        }
                    }
                }
                if (!bFoundEvenTreble)
                {
                    bool bSolFound = false;
                    // Use one dart to hit the required even-segment treble
                    for (int iter = 0; iter < trebleValues.Count && !bSolFound; iter++)
                    {
                        if ((trebleValues[iter] % 2) == 0)
                        {
                            // No point in checking if this treble busts us.
                            if (trebleValues[iter] < remainingScore)
                            {
                                bSolFound = bSolFound || PlayerHasPathToSolution(remainingScore - trebleValues[iter], dartsRemaining - 1, "T" + segValues[iter] + " ");
                            }
                        }
                    }
                    return bSolFound;
                }
            }

            // We only get here if there's nothing specific the user has to do. Try everything remaining.

            bool bSolFoundFromExhaustiveSearch = false;

            for (int singlesIter = 0; singlesIter < segValues.Count && !bSolFoundFromExhaustiveSearch; singlesIter++)
            {
                bSolFoundFromExhaustiveSearch = bSolFoundFromExhaustiveSearch || PlayerHasPathToSolution(remainingScore - segValues[singlesIter], dartsRemaining - 1, solutionSoFar + "S" + (singlesIter == 10 ? "B" : segValues[singlesIter].ToString()) + " ");
            }
            if (dartsRemaining > 2 || ((remainingScore % 2) == 0))
            {
                for (int doublesIter = 0; doublesIter < doubleValues.Count && !bSolFoundFromExhaustiveSearch; doublesIter++)
                {
                    bSolFoundFromExhaustiveSearch = bSolFoundFromExhaustiveSearch || PlayerHasPathToSolution(remainingScore - doubleValues[doublesIter], dartsRemaining - 1, solutionSoFar + "D" + (doublesIter == 10 ? "B" : segValues[doublesIter].ToString()) + " ");
                }
            }
            for (int treblesIter = 0; treblesIter < trebleValues.Count && !bSolFoundFromExhaustiveSearch; treblesIter++)
            {
                bSolFoundFromExhaustiveSearch = bSolFoundFromExhaustiveSearch || PlayerHasPathToSolution(remainingScore - trebleValues[treblesIter], dartsRemaining - 1, solutionSoFar + "T" + (treblesIter == 10 ? "B" : segValues[treblesIter].ToString()) + " ");
            }
            return bSolFoundFromExhaustiveSearch;
        }
    }

    private void RenderPlayerDart(int dartIndex, int buttonIndex)
    {
        DartObjects[dartIndex].SetActive(true);
        Transform childTransform = DartObjects[dartIndex].transform.GetChild(0);
        float radius = 0;
        if (buttonIndex < 10)
        {
            radius = 0.067f;
        }
        else if (buttonIndex < 20)
        {
            radius = 0.032f;
        }
        else if (buttonIndex < 30)
        {
            radius = 0.087f;
        }
        else if (buttonIndex < 40)
        {
            radius = 0.049f;
        }
        else if (buttonIndex == 40)
        {
            radius = 0.013f;
        }
        else if (buttonIndex == 41)
        {
            radius = 0.004f;
        }

        float angle = 0;
        if (buttonIndex < 40)
        {
            angle = Mathf.PI / 2 - (Mathf.PI / 5 * (buttonIndex % 10)) + Random.Range(Mathf.PI / -14, Mathf.PI / 14);
        }
        else
        {
            angle = 2 * Mathf.PI * Random.Range(0f, 1f);
        }
        childTransform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        childTransform.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f));
    }

    public string TwitchHelpMessage = "Select segments with !{0} throw (SegmentName). Use IN and OUT for singles (e.g. IN6, OUT20), D for doubles (D16), T for trebles (T13), SB and DB for single and double bullseye. You can select multiple segments at a time (e.g. \"!{0} throw T3 OUT15 D20\")";
    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] parts = command.ToUpper().Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts[0].Equals("THROW") || parts[0].Equals("PRESS"))
        {
            bool noErrors = true;
            List<int> buttonsToPress = new List<int>();
            for (int iter = 1; iter < parts.Length; iter++)
            {
                if (parts[iter] == "SB")
                {
                    buttonsToPress.Add(40);
                }
                else if (parts[iter] == "DB")
                {
                    buttonsToPress.Add(41);
                }
                else if (parts[iter].StartsWith("OUT"))
                {
                    int segVal = -1;
                    if (int.TryParse(parts[iter].Substring(3), out segVal))
                    {
                        int segValIndex = GetSegIndexForValue(segVal);
                        if (segValIndex > -1)
                        {
                            buttonsToPress.Add(segValIndex);
                        }
                        else
                        {
                            noErrors = false;
                        }
                    }
                    else
                    {
                        noErrors = false;
                    }
                }
                else if (parts[iter].StartsWith("IN"))
                {
                    int segVal = -1;
                    if (int.TryParse(parts[iter].Substring(2), out segVal))
                    {
                        int segValIndex = GetSegIndexForValue(segVal);
                        if (segValIndex > -1)
                        {
                            buttonsToPress.Add(10 + segValIndex);
                        }
                        else
                        {
                            noErrors = false;
                        }
                    }
                    else
                    {
                        noErrors = false;
                    }
                }
                else if (parts[iter].StartsWith("D"))
                {
                    int segVal = -1;
                    if (int.TryParse(parts[iter].Substring(1), out segVal))
                    {
                        int segValIndex = GetSegIndexForValue(segVal);
                        if (segValIndex > -1)
                        {
                            buttonsToPress.Add(20 + segValIndex);
                        }
                        else
                        {
                            noErrors = false;
                        }
                    }
                    else
                    {
                        noErrors = false;
                    }
                }
                else if (parts[iter].StartsWith("T"))
                {
                    int segVal = -1;
                    if (int.TryParse(parts[iter].Substring(1), out segVal))
                    {
                        int segValIndex = GetSegIndexForValue(segVal);
                        if (segValIndex > -1)
                        {
                            buttonsToPress.Add(30 + segValIndex);
                        }
                        else
                        {
                            noErrors = false;
                        }
                    }
                    else
                    {
                        noErrors = false;
                    }
                }
                else
                {
                    noErrors = false;
                }
            }
            if (noErrors)
            {
                KMSelectable[] retButtons = new KMSelectable[buttonsToPress.Count];
                for (int iter = 0; iter < buttonsToPress.Count; iter++)
                {
                    retButtons[iter] = Buttons[buttonsToPress[iter]];
                }
                int buttonsFromThisCommandThatHaveBeenPressed = 0;

                yield return null;
                while (buttonsFromThisCommandThatHaveBeenPressed < buttonsToPress.Count)
                {
                    bool bButtonWasPressedBeforeCommand = buttonHasBeenPressed[buttonsToPress[buttonsFromThisCommandThatHaveBeenPressed]];

                    yield return retButtons[buttonsFromThisCommandThatHaveBeenPressed];
                    if (buttonsFromThisCommandThatHaveBeenPressed != retButtons.Length - 1)
                    {
                        yield return new WaitForSeconds(1.2f);
                    }
                    if (bButtonWasPressedBeforeCommand || buttonHasBeenPressed[buttonsToPress[buttonsFromThisCommandThatHaveBeenPressed]])
                    {
                        buttonsFromThisCommandThatHaveBeenPressed++;
                    }
                }
            }
        }

        yield break;
    }
    private int GetSegIndexForValue(int val)
    {
        int retIndex = -1;
        for (int iter = 0; iter < 10 && retIndex == -1; iter++)
        {
            if (segValues[iter] == val)
            {
                retIndex = iter;
            }
        }
        return retIndex;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        if (_pathBtns == null)
            _pathBtns = GetPathFromStrings(CorrectSolutions[0].Split(' '));
        while (!isModuleSolved)
        {
            Buttons[_pathBtns[0]].OnInteract();
            if (!isModuleSolved)
                yield return new WaitForSeconds(1.2f);
        }
    }

    private List<int> GetPathFromStrings(string[] path)
    {
        var p = new string[path.Length];
        var pathBtns = new List<int>();
        for (int i = 0; i < p.Length; i++)
        {
            p[i] = path[i];
        }
        for (int i = 0; i < p.Length; i++)
        {
            if (p[i] == "SB")
            {
                pathBtns.Add(40);
                continue;
            }
            if (p[i] == "DB")
            {
                pathBtns.Add(41);
                continue;
            }
            int num;
            int.TryParse(p[i].Substring(1), out num);
            int ix = Array.IndexOf(segValues.ToArray(), num);
            int offset = 0;
            if (p[i][0] == 'S' && buttonHasBeenPressed[ix])
                offset = 10;
            if (p[i][0] == 'D')
                offset = 20;
            if (p[i][0] == 'T')
                offset = 30;
            pathBtns.Add(offset + ix);
        }
        return pathBtns;
    }
}
