using KModkit;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Serialization;


public class GiantsCipher : MonoBehaviour {

	// Static data
    /// <summary> All 64 words that can appear on the module, in forward binary order (000000, 000001, 000010, etc.) </summary>
	readonly string[] orderedHintList = new string[]{"ACACIA", "MADMAN", "ACHING", "FALCON", "BALSAM", "MAGNET", "MAGNUM", "HADRON", "GERBIL", "EASILY", "CARING", "ABSENT", "KETTLE", "BANNER", "BASQUE", "KAZOOS", "COFFEE", "HOBBIT", "ANALOG", "BRAINS", "ERASED", "DRIVEN", "FOGRUM", "COLORS", "ATOMIC", "LUNACY", "JOYFUL", "LONDON", "INSTIL", "AUTUMN", "CONSUL", "CONVOY", "ZAGGED", "TABLES", "NAMING", "SADIST", "OBEYED", "RAISES", "VACUUM", "REBOOT", "ULTIMA", "OBTAIN", "TAXING", "NINETY", "TAPPED", "ZIPPER", "THRONE", "NEURON", "QUEBEC", "QUACKS", "WRAITH", "QUEENS", "TRIPLE", "TRASHY", "QUINOA", "ROBOTS", "WORKED", "VOWELS", "OWNING", "NOTION", "TROUGH", "VORTEX", "SPOUSE", "SORROW"};
	/// <summary> The first 13 letters of the Alphabet, for use in Cipher R </summary>
    readonly char[] startAlphabet = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M' };
	/// <summary> The end/start grid for the 5 possible Regis: Rock, Ice, Steel, Eleki, Drago </summary>
    readonly string[] possibleResults = new string[] { ".X.X..XXX..X.X.", "..X..XXXXX..X..", ".X.X.X.X.X.X.X.", "X...X.XXX.X...X", "X.X.X.XXX...X.."};
    /// <summary> Array representing the 6 LEDs that get changed by Cipher R. To be offset by 0-3 to start at columns A-D. </summary>
    readonly int[] CipherRDoubleColumnLookup = new int[6] { 0, 1, 5, 6, 10, 11 };
    /// <summary> The 11 possible rules used in ordering the Ciphers </summary>
    readonly string[] CipherOrderingRules = new string[11]
    {
        "the number of unlit indicators",
        "the number of Serial Number digits",
        "the number of lit indicators",
        "the number of batteries",
        "the number of port plates",
        "the number of Serial Number letters",
        "the number of ports",
        "the number of battery holders",
        "the number of modules, plus or minus 7 until in the range 0-6",
        "the last digit of the Serial Number",
        "the first digit of the Serial Number"
    };
    /// <summary> The 9 possible grids used for Xor Steps (Cipher E and potential step in Cipher D) </summary>
    readonly string[] possibleXorGrids = new string[9] {"..X...XXX.X.X.X", ".XXX.X...X.XXX.", "X.X.X.X.X.X.X.X", "XX.XX..X...X.X.", "..X..XX.XXX...X", "X.X.XX...XX.X.X", "X..X.X.XXXX..X.", "XX.XX.....XX.XX", "XX....XXX....XX"};


    // Target & Current Data
    /// <summary> What is the expected answer in order: 0-Regirock, Regice, Registeel, Regieleki, and Regidrago as 4 </summary>
    int resultID;
    /// <summary> Representation of the current 5x3 Canvas that gets modified over time </summary>
	string currentCanvas;
    /// <summary> The order in which the player has to apply the Decryption ciphers (on the manual) to get an answer </summary>
	string DecryptionCipherOrder;
    /// <summary> The order in which the module will apply the Encryption ciphers (inverse of manual) to create the puzzle </summary>
    string EncryptionCipherOrder;
    /// <summary> Selected Keyword shown on the module, for Cipher R </summary>
    string SelectedKeyword;


    // All of the messed-up Ruleseed shuffling thinggy 
    /// <summary> Which 5 Cipher Ordering Rules have been decided by ruleseed, to order the RISED Ciphers </summary>
    int[] selectedCipherOrderingRules;
    /// <summary> The order in which the Ciphers must be ordered if they have ties after using the Ordering Rules </summary>
    string cipherTiePriorityOrder;
    /// <summary> If true, A-M are represented by a 0 and N-Z by a 1; if false, the opposite is true. </summary>
    bool RCipherUseRegularOrder;
    /// <summary> The two possible starting columns for R Cipher replacement, in range [0; 3] </summary>
    Vector2Int selectedRCipherStartingColumns;
    /// <summary> Grid of shuffling used by I Cipher </summary>
    int[] ICipherShufflingGrid;
    /// <summary> Xor grid to use in the Cipher E step </summary>
    int selectedECipherXorGrid;
    /// <summary> Xor grid to use in the POTENTIAL Cipher D step (might not actually be used) </summary>
    int selectedDCipherXorGrid;
    /// <summary> The 5 Cipher D rules that can appear in order. Rule index 4 should NEVER appear before rule 0!!! </summary>
    List<int> selectedDCipherRules;
    /// <summary> Extremely Dangerous list containing all of the arguments necessary to make Cipher D function. Please verify all data before using it. </summary>
    List<object> DCipherArguments;
    /// <summary> Index to be used over multiple steps, it starts at the last index inside DCipherArguments (since we encrypt in reverse), and goes down. </summary>
    int currentDCipherArgumentsIndex;

    // Bomb & Module Variables
    [FormerlySerializedAs("bombInfos")] public KMBombInfo bombInfo;
    public KMRuleSeedable ruleseedManager;
    public KMBombModule thisBombModule;
    public KMAudio audioSystem;
    public TextMesh keywordTextMesh;
    public List<MeshRenderer> allLeds;
    public Material LedOnMaterial, LedOffMaterial;
    public KMSelectable[] pressableButtons;
    public AudioClip regirockSound, regiceSound, registeelSound, regielekiSound, regidragoSound;


    // Logging Data - Formatting & naming from Royal_Flu$h
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    /// <summary> Since the Encryption (module) is done backwards relative to the Decryption (player), save all logging and then play it backwards. </summary>
    List<string> fullLog;


    // Buttons gathering and GetComponents
    void Awake()
	{
        moduleId = moduleIdCounter++;

        foreach (KMSelectable button in pressableButtons)
        {
            button.OnInteract += delegate () { PatternGetsPressed(Array.IndexOf(pressableButtons, button), button); return false; };// 0 Regirock - 1 Regice - 2 Registeel - 3 Regieleki - 4 Regidrago
        }
    }


    // Puzzle Initialization
    void Start ()
	{
        ModuleLog(true, "Starting Initialization.");
        fullLog = new List<string>();

        InitializeModule();

        keywordTextMesh.text = SelectedKeyword;

        for (int i = 0; i < 15; i++)
        {
            allLeds[i].material = currentCanvas[i] == 'X' ? LedOnMaterial : LedOffMaterial;
        }
    }

    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //  Player Interaction
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=



    void PatternGetsPressed(int patternIndex, KMSelectable pressedButton) // 0 Regirock - 1 Regice - 2 Registeel - 3 Regieleki - 4 Regidrago
    {
        if (moduleSolved)
        { return; }
        pressedButton.AddInteractionPunch(0.6f);

        ModuleLog(true, "Pressed button with pattern:");
        PrintCanvasToLog(false, possibleResults[patternIndex]);

        if (patternIndex == resultID)
        {

            HandleSolve();
        }
        else
        {
            HandleStrike();
        }
    }

    void HandleSolve()
    {
        ModuleLog(true, "That is Correct! Module solved!");

        AudioClip _soundToPlay = null;

        switch (resultID)
        {
            case 0:
                _soundToPlay = regirockSound;
                break;

            case 1:
                _soundToPlay = regiceSound;
                break;

            case 2:
                _soundToPlay = registeelSound;
                break;

            case 3:
                _soundToPlay = regielekiSound;
                break;

            case 4:
                _soundToPlay = regidragoSound;
                break;
        }

        audioSystem.PlaySoundAtTransform(_soundToPlay.name, transform);

        keywordTextMesh.text = "WELL DONE";

        StartCoroutine(LightSolvePatternLights());

        moduleSolved = true;
        thisBombModule.HandlePass();
    }

    void HandleStrike()
    {
        ModuleLog(true, "That is Wrong! !i!i! STRIKE !i!i!");
        thisBombModule.HandleStrike();
    }


    IEnumerator LightSolvePatternLights()
    {
        // Entire "Turn Off then Turn On" sequence to hide starting information
        // Adds some cool solve feedback, and allow Souvenir questions

        // Determine which lights should be turned on in advance
        string correctResult = possibleResults[resultID];
        int[] lightsToTurnOnID = new int[7] { 0, 0, 0, 0, 0, 0, 0 };
        int numberOfLightsRegistered = 0;

        // Turn off all lights
        // While we're doing a loop, register the Light IDs
        for (int i = 0; i < 15; i++)
        {
            allLeds[i].material = LedOffMaterial;
            if (correctResult[i] == 'X')
            {
                lightsToTurnOnID[numberOfLightsRegistered] = i;
                numberOfLightsRegistered++;
            }
        }

        yield return new WaitForSeconds(0.35f);


        // Turn on each light, in reading order, one by one
        for (int i = 0; i < 7; i++)
        {
            allLeds[lightsToTurnOnID[i]].material = LedOnMaterial;
            yield return new WaitForSeconds(0.15f);
        }

    }




    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //  Puzzle Initialization
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=


    void InitializeModule()
    {
        VerifyDataIntegrity();

        ManageRulesed();
        DetermineCipherOrder();

        // Determine what is the expected target result
        resultID = UnityEngine.Random.Range(0, 5);
        currentCanvas = possibleResults[resultID];

        fullLog.Add(currentCanvas);
        fullLog.Add("tPress this pattern to solve the module:");

        EncryptMessage();

        fullLog.Add(currentCanvas);
        fullLog.Add("tStarting Canvas shown on module:");
        fullLog.Add("tKey Word shown on module: " + SelectedKeyword);

        // Log everything by iterating through the log in reverse
        string _log;
        for (int i = fullLog.Count - 1; i >= 0; i--)
        {
            _log = fullLog[i];
         
            // Log to the true LFA or not depending if the first character is a t or not
            if (_log[0] == 't')
            {
                ModuleLog(true, _log.Substring(1));
            }
            else if (_log[0] == 'f')
            {
                ModuleLog(false, _log.Substring(1));
            }
            else if (_log[0] == 'X' || _log[0] == '.')
            {
                // Log of the canvas!
                ModuleLog(true, _log.Substring(0, 5));
                ModuleLog(true, _log.Substring(5, 5));
                ModuleLog(true, _log.Substring(10, 5));
            }
            else
            {
                Debug.LogErrorFormat("[Giants Cipher #{0}] Unknown Log ''{1}'' doesn't start with a t or f. Please report to thunder725", moduleId, _log);
            }
         
        }
    }

    void ManageRulesed()
    {
        MonoRandom Rng = ruleseedManager.GetRNG();

        ModuleLog(true, "Using Ruleseed {0}:", Rng.Seed);

        if (Rng.Seed == 1)
        {
            // Initialize the default game-designed values for ruleseed 1
            selectedCipherOrderingRules = new int[5] { 0, 1, 2, 3, 4 };
            cipherTiePriorityOrder = "RISED";

            RCipherUseRegularOrder = true;
            selectedRCipherStartingColumns = new Vector2Int(0, 3);

            ICipherShufflingGrid = new int[8] { 0, 11, 13, 4, 2, 5, 9, 12 };

            selectedECipherXorGrid = 0;

            selectedDCipherXorGrid = 1;
            selectedDCipherRules = new List<int> { 0, 1, 2, 3, 4 };
            DCipherArguments = new List<object>()
            {
                'C', 3, // Step 1 - Outer Edges Rotation - Clockwise Thrice
                1, 'R', // Step 2 - Horizontal Shifting number of D steps - Row number 2 to the Right
                // Step 3 - no arguments, it's the XOR grid
                1, 180, // Step 4 - Square Clockwise Rotation - Start in column number 2, rotate 180°
                1, 'W', 3 // Step 5 - Reverse Outer Edge Rotation - Reverse of rule 1, go Widdershins Thrice
            };

            return;
        }

        // Cipher Ordering Rules
        selectedCipherOrderingRules = Rng.ShuffleFisherYates(new int[11] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }).Take(5).ToArray();
        cipherTiePriorityOrder = Rng.ShuffleFisherYates(new char[5] { 'R', 'I', 'S', 'E', 'D' }).Join("");

        ModuleLog(false, "Cipher Rules used in order are {0}, with tie order {1}.", selectedCipherOrderingRules.Join(), cipherTiePriorityOrder);


        // R Cipher
        RCipherUseRegularOrder = Rng.Next(0, 2) == 0;
        int[] _rLocations = Rng.ShuffleFisherYates(new int[4] { 0, 1, 2, 3 });
        selectedRCipherStartingColumns = new Vector2Int(_rLocations[0], _rLocations[1]);

        ModuleLog(false, "R Cipher will {0} use regular A-M order, with REGI rule replacing columns {1}-{2} or {3}-{4}.",
            RCipherUseRegularOrder ? "" : "NOT", 
            startAlphabet[selectedRCipherStartingColumns[0]], startAlphabet[selectedRCipherStartingColumns[0] + 1],
            startAlphabet[selectedRCipherStartingColumns[1]], startAlphabet[selectedRCipherStartingColumns[1] + 1]);


        // I Cipher
        ICipherShufflingGrid = Rng.ShuffleFisherYates(new int[15] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }).Take(8).ToArray();

        ModuleLog(false, "I Cipher will use grid with digits in indices {0}", ICipherShufflingGrid.Join());



        // E Cipher
        int[] _xorGrids = Rng.ShuffleFisherYates(new int[9] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
        selectedECipherXorGrid = _xorGrids[0];

        ModuleLog(false, "E Cipher will use grid {0}", selectedECipherXorGrid);



        // D Cipher
        selectedDCipherXorGrid = _xorGrids[1];
        // Rule index 4 "Inverse of Step 1" can't appear before that "Step 1"; however that step 1 can happen any time
        // So we shuffle without that rule, and insert it randomly AFTER it
        selectedDCipherRules = Rng.ShuffleFisherYates(new List<int>() { 0, 1, 2, 3, 5, 6 });
        selectedDCipherRules.Insert(Rng.Next(selectedDCipherRules.IndexOf(0) + 1, selectedDCipherRules.Count), 4);
        selectedDCipherRules = selectedDCipherRules.Take(5).ToList();

        ModuleLog(false, "D Cipher will use grid {0}; and rules {1} in order", selectedDCipherXorGrid, selectedDCipherRules.Join());


        DCipherArguments = new List<object>();

        // Make list of Arguments for D Cipher
        char[] _clockRotations = new char[2] { 'C', 'W' };
        char[] _horizontalDirections = new char[2] { 'R', 'L' };
        char[] _verticalDirections = new char[2] { 'D', 'U' };
        char[] _flipDirections = new char[2] { 'H', 'V' };

        // Memory locations in case step Type One and step Type Five are there at the same time
        // so they're inverse of each other
        int stepTypeOneAmount = -1;
        bool stepTypeOneIsClockwise = true;
        for (int i = 0; i < 5; i ++)
        {
            switch (selectedDCipherRules[i])
            {
                case 0: // Rotate Outer [Clockwise, Widdershins] a total of [1-5] times
                    stepTypeOneIsClockwise = Rng.Next(0, 2) == 0;
                    stepTypeOneAmount = Rng.Next(0, 5) + 1;
                    DCipherArguments.Add(stepTypeOneIsClockwise ? _clockRotations[0]: _clockRotations[1]);
                    DCipherArguments.Add(stepTypeOneAmount);
                    break;
                case 1: // Shift Row index [0-2] [Left, Right] the same number of times as D-Cipher steps you'll do
                    DCipherArguments.Add(Rng.Next(0, 3));
                    DCipherArguments.Add(_horizontalDirections[Rng.Next(0, 2)]);
                    break;
                case 2: // "Xor with this grid" - No Arguments
                    break;
                case 3: // Rotate the Square using Columns index [0-2] and its n+1 & n+2 a total of [90, 180, 270]° clockwise
                    DCipherArguments.Add(Rng.Next(0, 3));
                    DCipherArguments.Add(Rng.Next(1, 4) * 90);
                    break;
                case 4: // Do the opposite of [Step Case 0]: Rotate Outer [Clockwise, Widdershins] a total of [1-5] times
                    DCipherArguments.Add(selectedDCipherRules.IndexOf(0) + 1);
                    DCipherArguments.Add(stepTypeOneIsClockwise ? _clockRotations[1]: _clockRotations[0]);
                    DCipherArguments.Add(stepTypeOneAmount);
                    break;
                case 5: // Shift the whole grid [Up, Down] [1-2] times
                    DCipherArguments.Add(_verticalDirections[Rng.Next(0, 2)]);
                    DCipherArguments.Add(Rng.Next(0, 2) + 1);
                    break;
                case 6: // Flip the canvas [Horiontally, Vertically]
                    DCipherArguments.Add(_flipDirections[Rng.Next(0, 2)]);
                    break;
            }
        }

        ModuleLog(false, "D Cipher arguments array is {0}", DCipherArguments.Join(" // "));
    }

    void VerifyDataIntegrity()
	{
        ModuleLog(false, "Verifying Data Integrity.");

        // Possible Regi Results
		foreach (string _regi in possibleResults)
		{
			if (_regi.Length !=15)
			{
                ModuleLog(false, "REGI {0} DOESN'T HAVE 15 CHARCTERS BUT {1}", _regi, _regi.Length);
            }
		}
        ModuleLog(false, "All Regi Patterns have been checked.");


        // Xor Grids
        foreach (string _Xor in possibleXorGrids)
        {
            if (_Xor.Length != 15)
            {
                ModuleLog(false, "XOR GRID {0} DOESN'T HAVE 15 CHARCTERS BUT {1}", _Xor, _Xor.Length);
            }
        }
        ModuleLog(false, "All XOR Grids have been checked.");


        // R Cipher Wordlist
        char[] letters = new char[6];
		int value;

		for (int i = 0; i < orderedHintList.Length; i++) 
		{
			// Reset value
            value = 0;

            letters = orderedHintList[i].ToCharArray();
			if (letters.Length != 6)
			{
                ModuleLog(false, "WORD {0} DOESN'T HAVE 6 CHARACTERS BUT HAS {1}", letters, letters.Length);
				continue;
			}

			// Check every letter
			for (int j = 0; j < 6; j++)
			{
                if (!startAlphabet.Contains(letters[j]))
                {
					value += (int)Mathf.Pow(2, 5 - j);
                }
            }

			
			if (value != i)
			{
                ModuleLog(false, "WORD {0} DOESN'T MATCH ITS NUMBER {1} BUT IS ACTUALLY {2}", letters, i, value);
			}
		}


        // DCipher Rotations
        // Start with the canvas 
        // X X . X .
        // . . . X X
        // . X . . .
        // Rotate it multiple times and check
        currentCanvas = "XX.X....XX.X...";

        // Clockwise 3 gives (we input the opposite since it's made to encrypt and not decrypt)
        // X . . X X
        // . . . X .
        // . . X . X
        CipherDOuterRotation('W', 3, false);
        if (currentCanvas != "X..XX...X...X.X")
        {
            ModuleLog(false, "FIRST OUTER ROTATION CLOCK-3 HAS RETURNED {0} INSTEAD OF {1}!!", currentCanvas, "X..XX...X...X.X");
        }

        // Counter 5 gives
        // . X . X .
        // X . . X .
        // X . . X .
        CipherDOuterRotation('C', 5, false);
        if (currentCanvas != ".X.X.X..X.X..X.")
        {
            ModuleLog(false, "SECOND OUTER ROTATION WIDDER-5 HAS RETURNED {0} INSTEAD OF {1}!!", currentCanvas, ".X.X.X..X.X..X.");
        }

        // Clock 2 returns to start
        CipherDOuterRotation('W', 2, false);
        if (currentCanvas != "XX.X....XX.X...")
        {
            ModuleLog(false, "THIRD OUTER ROTATION CLOCK-2 HAS RETURNED {0} INSTEAD OF {1}!!", currentCanvas, "XX.X....XX.X...");
        }
        ModuleLog(false, "All D-Cipher Outer Rotations have been checked.");


        // We stay with the previous starting canvas, and just rotate a bunch

        // . X . X .
        // . . . X X
        // . X X . .
        CipherD3x3SquareRotation(0, 180, false);
        if (currentCanvas != ".X.X....XX.XX..")
        {
            ModuleLog(false, "FIRST 3x3 ROTATION ABC 180 HAS RETURNED {0} INSTEAD OF {1}!!", currentCanvas, ".X.X....XX.XX..");
        }

        // . X X . .
        // . . . X X
        // . X . X .
        CipherD3x3SquareRotation(1, 90, false);
        if (currentCanvas != ".XX.....XX.X.X.")
        {
            ModuleLog(false, "SECOND 3x3 ROTATION BCD-90 HAS RETURNED {0} INSTEAD OF {1}!!", currentCanvas, ".XX.....XX.X.X.");
        }

        // . X . . X
        // . . X X .
        // . X . X .
        CipherD3x3SquareRotation(2, 270, false);
        if (currentCanvas != ".X..X..XX..X.X.")
        {
            ModuleLog(false, "THIRD 3x3 ROTATION DCE-270 HAS RETURNED {0} INSTEAD OF {1}!!", currentCanvas, ".X..X..XX..X.X.");
        }

        ModuleLog(false, "All D-Cipher 3x3 Rotations have been checked.");

        ModuleLog(false, "Data Validation Completed");
	}

	void DetermineCipherOrder()
	{
		// Reset order string to five empty slots
        DecryptionCipherOrder = ".....";

        Func<int>[] rules = new Func<int>[11]
        {
            () => bombInfo.GetOffIndicators().Count(),
            () => bombInfo.GetSerialNumberNumbers().Count(),
            () => bombInfo.GetOnIndicators().Count(),
            () => bombInfo.GetBatteryCount(),
            () => bombInfo.GetPortPlateCount(),
            () => bombInfo.GetSerialNumberLetters().Count(),
            () => bombInfo.GetPorts().Count(),
            () => bombInfo.GetBatteryHolderCount(),
            () => bombInfo.GetSolvableModuleIDs().Count() % 7,
            () => bombInfo.GetSerialNumberNumbers().Last(),
            () => bombInfo.GetSerialNumberNumbers().First()
        };




        // Apply the 5 ruleseeded rules in order


        int cipherRScore = rules[selectedCipherOrderingRules[0]].Invoke();
        ModuleLog(true, "Cipher R score ({0}) is {1}.", CipherOrderingRules[selectedCipherOrderingRules[0]], cipherRScore);

        int cipherIScore = rules[selectedCipherOrderingRules[1]].Invoke();
        ModuleLog(true, "Cipher I score ({0}) is {1}.", CipherOrderingRules[selectedCipherOrderingRules[1]], cipherIScore);

        int cipherSScore = rules[selectedCipherOrderingRules[2]].Invoke();
        ModuleLog(true, "Cipher S score ({0}) is {1}.", CipherOrderingRules[selectedCipherOrderingRules[2]], cipherSScore);

        int cipherEScore = rules[selectedCipherOrderingRules[3]].Invoke();
        ModuleLog(true, "Cipher E score ({0}) is {1}.", CipherOrderingRules[selectedCipherOrderingRules[3]], cipherEScore);

        int cipherDScore = rules[selectedCipherOrderingRules[4]].Invoke();
        ModuleLog(true, "Cipher D score ({0}) is {1}.", CipherOrderingRules[selectedCipherOrderingRules[4]], cipherDScore);

        // Sort them in descending order.
        int[] _numberOrder = new int[5] { cipherRScore, cipherIScore, cipherSScore, cipherEScore, cipherDScore };
        Array.Sort(_numberOrder);
        _numberOrder = _numberOrder.Reverse().ToArray();

        // Helper Dictionnary
        Dictionary<char, int> letterToScore = new Dictionary<char, int>() {
            { 'R', cipherRScore},
            { 'I', cipherIScore},
            { 'S', cipherSScore},
            { 'E', cipherEScore},
            { 'D', cipherDScore} };

        // Then retro-determine the placement of each Cipher, giving priority to RISED in an order determined by ruleseed (RISED being default).
        // For that, loop through the number order, skipping Ciphers that already exist, and assign them to the CipherOrder
        for (int i = 0; i < 5; i++)
		{
            // This is the next highest value found here
			int _nextCipherScore = _numberOrder[i];

            // Go through the Tie Order, and verify if the associated value is this one
            for (int j = 0; j < 5; j++)
            {
                // If Cipher E hasn't appeared yet, and Cipher E has the correct number, it HAS to be the correct choice
                // Since we go in the tie order, it's guaranteed to be correct
                if (DecryptionCipherOrder.Contains(cipherTiePriorityOrder[j]) == false && _nextCipherScore == letterToScore[cipherTiePriorityOrder[j]])
                {
                    DecryptionCipherOrder = DecryptionCipherOrder.Remove(i, 1).Insert(i, cipherTiePriorityOrder[j].ToString());
                    break;
                }
            }
        }

        EncryptionCipherOrder = new string(DecryptionCipherOrder.Reverse().ToArray());

        ModuleLog(true, "The final sorted Cipher Order is {0}.", DecryptionCipherOrder);
        ModuleLog(false, "The module will now encrypt the canvas using reversed order {0}.", EncryptionCipherOrder);
    }

    void EncryptMessage()
    {
        char[] _encryptionOrder = EncryptionCipherOrder.ToCharArray();

        foreach (char _nextCipher in _encryptionOrder)
        {
            switch (_nextCipher)
            {
                case 'R':
                    EncryptCipherR();
                    break;


                case 'I':
                    EncryptCipherI();
                    break;


                case 'S':
                    EncryptCipherS();
                    break;


                case 'E':
                    EncryptCipherE();
                    break;


                case 'D':
                    EncryptCipherD(5 - DecryptionCipherOrder.IndexOf('D')); // Power is in range 1-5
                    break;
            }
        }
    }

    void EncryptCipherR()
    {
        // To Encrypt using R:
        // Determine which two columns to use
        // Note down the current 2x3 state as a binary number 0-63
        // Take the word in orderedHintList that corresponds to that binary number
        // Replace the side with random LED states

        string startingCanvas = currentCanvas;

        // This translates to "does the string _allIndicators contain at least a character from the pool R E G I" ?
        var _allIndicators = string.Concat(bombInfo.GetIndicators().ToArray()).ToUpperInvariant();
        bool _IsRegiPresent = Regex.IsMatch(_allIndicators, "[REGI]");


        int columnToUse = _IsRegiPresent ? selectedRCipherStartingColumns[0] : selectedRCipherStartingColumns[1];

        int _selectedSideScore = 0;

        // Use the baseIndicesToCheckCipherR in order
        // If we should replace right however, offset every index by +3 to go to the right side
        int _startIndex = columnToUse;
        char _currentLight;
        string _sideLinearRepresentation = "";

        // Depending on ruleseed, ON or OFF can count as a "later part of the alphabet"
        char _matchCharacter = RCipherUseRegularOrder ? 'X' : '.';

        for (int i = 0; i < 6; i ++)
        {
            // Read the current light
            _currentLight = currentCanvas[CipherRDoubleColumnLookup[i] + _startIndex];
            _sideLinearRepresentation += _currentLight;

            // Update the binary value to determine which word to use
            if (_currentLight == _matchCharacter)
            {
                _selectedSideScore += (int)Mathf.Pow(2, 5 - i);
            }

            // Randomly scramble the Canvas, the information is stored in the Keyword so we don't need it anymore and can randomize it
            if (UnityEngine.Random.value > 0.5)
            {
                FlipLightInCanvas(CipherRDoubleColumnLookup[i] + _startIndex);
                // Debug.LogFormat("[Giants Cipher #{0}] Randomly decided to flip light with index {1}", moduleId, CipherRDoubleColumnLookup[i] + _startIndex);
            }
        }

        SelectedKeyword = orderedHintList[_selectedSideScore];

        fullLog.Add(startingCanvas);
        fullLog.Add(string.Format("tPlacing that sequence (as a 2x3 vertical rectangle) into columns {0}-{1} results in Canvas:",
            startAlphabet[columnToUse], startAlphabet[columnToUse + 1]));
        fullLog.Add(string.Format("t{0} Indicator containing a letter from REGI. Columns to replace are {1}-{2}",
            _IsRegiPresent ? "Found" : "Did not find", startAlphabet[columnToUse], startAlphabet[columnToUse + 1]));
        fullLog.Add(string.Format("tKeyword {0} is transformed into the linear sequence of lights {1}", SelectedKeyword, _sideLinearRepresentation));
        fullLog.Add("t=-= Beginning Cipher R =-=");
    }

    void EncryptCipherI()
    {
        // To Encrypt using I:
        // Take the table in the manual,
        // but swap 8 into 7, 7 into 6, 6 into 5, ... and 1 into 8

        string startingCanvas = currentCanvas;

        // ICipherShufflingGrid has 8 elements, being the indices of 1,2,3...8 on the grid
        // Since we want to reverse them; 8 goes to 7, 7 to 6... and then 1 to 8 at the end
        int _currentLightIndex;
        char _previousLight = currentCanvas[ICipherShufflingGrid[7]];
        char _currentLight;

        for (int i = 6; i >= 0; i --)
        {
            _currentLightIndex = ICipherShufflingGrid[i];
            _currentLight = currentCanvas[_currentLightIndex];
            currentCanvas = currentCanvas.Remove(_currentLightIndex, 1).Insert(_currentLightIndex, _previousLight.ToString());

            _previousLight = _currentLight;
        }

        // We've replaced all, except for 1 to 8
        currentCanvas = currentCanvas.Remove(ICipherShufflingGrid[7], 1).Insert(ICipherShufflingGrid[7], _previousLight.ToString());

        fullLog.Add(startingCanvas);
        fullLog.Add("tAfter bringing the position #1 to #2, #2 to #3... and finally #8 to #1; the final Canvas is:");
        fullLog.Add("t=-= Beginning Cipher I =-=");
    }

    void EncryptCipherS()
    {
        // To Encrypt using S:
        // Read the line as binary 0-31
        // Add 32 until the number is a multiple of five
        // Divide by 5
        // Set the line as the result when read in binary
        // Yes, all numbers from 0-31 get encrypted to another unique number within 0-31 without loss

        string startingCanvas = currentCanvas;

        string topRow = currentCanvas.Remove(5, 10);
        string middleRow = currentCanvas.Remove(10, 5).Remove(0, 5);
        string bottomRow = currentCanvas.Remove(0, 10);

        bottomRow = ComputeCipherSNewRow(bottomRow, "Bottom Row");
        middleRow = ComputeCipherSNewRow(middleRow, "Middle Row");
        topRow = ComputeCipherSNewRow(topRow, "Top Row");

        currentCanvas = topRow + middleRow + bottomRow;

        // ComputeCipherSNewRow adds a total of 6 messages
        // We want to insert both of those values 6 before the last
        fullLog.Insert(fullLog.Count - 6, startingCanvas);
        fullLog.Insert(fullLog.Count - 6, "tResulting Canvas is:");
        fullLog.Add("t=-= Beginning Cipher S =-=");
    }

    string ComputeCipherSNewRow(string decryptedRow, string rowPrintName)
    {
        // Step 1 : Convert to Binary the decrypted Row
        char _currentLight;
        int decryptedBinaryResult = 0;

        for (int i = 0; i < 5; i++)
        {
            _currentLight = decryptedRow[i];
            if (_currentLight == 'X')
            {
                decryptedBinaryResult += (int)Mathf.Pow(2, 4 - i);
            }
        }

        // Step 2 : Add 32 until it's a multiplier of 5, then divide by 5 to get the Encrypted Binary
        int encryptedBinary = decryptedBinaryResult;
        while (encryptedBinary % 5 != 0)
        {
            encryptedBinary += 32;
        }

        encryptedBinary /= 5;


        // Step 3 : Convert back to binary to get an Encrypted row result
        string encryptedRow = "";
        int _workingEncryptedBinary = encryptedBinary;
        for (int i = 4; i >= 0; i --)
        {
            if (_workingEncryptedBinary >= Mathf.Pow(2, i))
            {
                encryptedRow += 'X';
                _workingEncryptedBinary -= (int)Mathf.Pow(2, i);
            }
            else
            {
                encryptedRow += '.';
            }
        }

        fullLog.Add(string.Format("tDecrypted binary is {0} which has representation {1}", decryptedBinaryResult, decryptedRow));
        fullLog.Add(string.Format("t{0}'s encrypted representation is {1}, which is binary for {2}.", rowPrintName, encryptedRow, encryptedBinary));
        return encryptedRow;
    }

    void EncryptCipherE()
    {
        // To Encrypt using E:
        // Just do the XOR again, it's a flip-flop

        // . . X . .
        // . X X X .
        // X . X . X
        // => 2 6 7 8 10 12 14

        string startingCanvas = currentCanvas;

        string gridToUse = possibleXorGrids[selectedECipherXorGrid];

        for (int i = 0; i < 15; i ++)
        {
            if (gridToUse[i] == 'X')
            {
                FlipLightInCanvas(i);
            }
        }

        fullLog.Add(startingCanvas);
        fullLog.Add("tAfter flipping all lights, the Canvas is:");
        fullLog.Add("t=-= Beginning Cipher E =-=");
    }

    void EncryptCipherD(int cipherPower) // Power is in range 1-5
    {
        // To Encrypt using D:
        // Do the steps in reverse order (3 -> 2 -> 1)

        currentDCipherArgumentsIndex = DCipherArguments.Count - 1;

        // Step down in power, start at 5, if we can do it, do it
        // Then go down to 4, and 3, then 2, then 1
        for (int i = 5; i > 0; i --)
        {
            if (cipherPower >= i)
            {
                // Give the Cipher Power, for the potential Rule 1 which asks for it
                ApplyCipherDRule(selectedDCipherRules[i - 1], cipherPower);

                fullLog.Add("tCipher D - Substep " + i);
            }
            else
            {
                // Cannot do this step; however we still need to read Arguments since we read them backwards!
                switch (selectedDCipherRules[i - 1])
                {
                    case 0: // Read Two
                        GetNewDCipherArgument();
                        GetNewDCipherArgument();
                        break;
                    case 1: // Read Two
                        GetNewDCipherArgument();
                        GetNewDCipherArgument();
                        break;
                    case 2: // None
                        break;
                    case 3: // Read Two
                        GetNewDCipherArgument();
                        GetNewDCipherArgument();
                        break;
                    case 4: // Read Three
                        GetNewDCipherArgument();
                        GetNewDCipherArgument();
                        GetNewDCipherArgument();
                        break;
                    case 5: // Read Two
                        GetNewDCipherArgument();
                        GetNewDCipherArgument();
                        break;
                    case 6: // Read One
                        GetNewDCipherArgument();
                        break;
                }
            }
        }

        string[] ordinals = new string[5] { "first", "second", "third", "fourth", "fifth"};
        fullLog.Add(string.Format("tIt is done as the {0} Cipher, so it will apply a total of {1} step{2}.",
            ordinals[5-cipherPower], cipherPower, cipherPower == 1 ? "" : "s"));
        fullLog.Add("t=-= Beginning Cipher D =-=");
    }

    void ApplyCipherDRule(int ruleIndex0To6, int cipherPower)
    {


        switch (ruleIndex0To6)
        {
            case 0: {
                    // Rotate the outer 12 lights {direction} {amount}
                    // Read the amount first then the clockness direction
                    int amountOfTurning = Convert.ToInt32(GetNewDCipherArgument());
                    char directionOfTurning = Convert.ToChar(GetNewDCipherArgument());

                    // Data Verification is managed in the rotation method
                    CipherDOuterRotation(directionOfTurning, amountOfTurning, true);
                    break;
                }


            case 1:
                {
                    string startingCanvas = currentCanvas;

                    // Shift row {index} {direction} the same amount as number of steps you'll do in Cipher D
                    // Read the direction first then row index
                    char directionOfShift = Convert.ToChar(GetNewDCipherArgument());
                    int rowIndex = Convert.ToInt32(GetNewDCipherArgument());

                    // Verify Data
                    if (directionOfShift != 'R' && directionOfShift != 'L')
                    {
                        fullLog.Add(string.Format("tDCipher argument DirectionOfShift for rule 1 is {0} when it should be R or L. Please report this to thunder725",
                            directionOfShift));
                    }
                    if (rowIndex < 0 || rowIndex > 2)
                    {
                        fullLog.Add(string.Format("tDCipher argument RowIndex for rule 1 is {0} when it should be 0-2. Please report this to thunder725",
                            rowIndex));
                    }


                    // Row to shift
                    string _rowToShift = currentCanvas.Substring(rowIndex * 5, 5);

                    // Shift
                    for (int i = 0; i < cipherPower; i++)
                    {
                        // R and L are inverted because we're ENCRYPTING while arguments are DECRYPTING
                        if (directionOfShift == 'R')
                        {
                            // This shifts leftward, by removing the first and adding at the end
                            _rowToShift = _rowToShift.Remove(0, 1) + _rowToShift[0];
                        }
                        else
                        {
                            // This shifts rightward, by removing the last and adding at the start
                            _rowToShift = _rowToShift[4] + _rowToShift.Remove(4, 1);
                        }
                    }

                    // Re-place the row
                    currentCanvas = currentCanvas.Remove(rowIndex * 5, 5).Insert(rowIndex * 5, _rowToShift);


                    fullLog.Add(startingCanvas);
                    fullLog.Add(string.Format("tShifting row number {0} {1} time{2} (equivalent to number of Cipher D sub-steps) to the {3}, resulting in:",
                        rowIndex+1, cipherPower, cipherPower == 1 ? "" : "s", directionOfShift == 'R' ? "Right" : "Left"));
                    break;
                }


            case 2: 
                {
                    string startingCanvas = currentCanvas;

                    // Canvas XORing - No arguments to read
                    string gridToUse = possibleXorGrids[selectedDCipherXorGrid];

                    for (int i = 0; i < 15; i++)
                    {
                        if (gridToUse[i] == 'X')
                        {
                            FlipLightInCanvas(i);
                        }
                    }

                    fullLog.Add(startingCanvas);
                    fullLog.Add("tAfter flipping all lights, the Canvas is:");
                    break;
                }


            case 3: 
                {
                    // Rotate a 3x3 square starting in {column index} an amount of {degrees} clock
                    // Read the degrees first then leftmost index of the square
                    int rotationAmount = Convert.ToInt32(GetNewDCipherArgument());
                    int leftmostColumnIndex = Convert.ToInt32(GetNewDCipherArgument());

                    // Verify Data is in the rotation
                    CipherD3x3SquareRotation(leftmostColumnIndex, rotationAmount, true);

                    break;
                }


            case 4:
                {   
                    // Do the opposite of case 0: Rotate the outer 12 lights {direction} {amount}
                    // Read the amount first then the clockness direction
                    int amountOfTurning = Convert.ToInt32(GetNewDCipherArgument());
                    char directionOfTurning = Convert.ToChar(GetNewDCipherArgument());

                    // Data Verification is managed in the rotation method
                    CipherDOuterRotation(directionOfTurning, amountOfTurning, true);

                    // There's also another argument that is useless for encryption but is used for logging
                    GetNewDCipherArgument();
                    break;
                }


            case 5:
                {
                    string startingCanvas = currentCanvas;

                    // Shift every row {up-down} {amount}
                    // Read amount then vertical direction
                    int shiftAmount = Convert.ToInt32(GetNewDCipherArgument());
                    char directionOfVerticalShift = Convert.ToChar(GetNewDCipherArgument());

                    // Verify Data
                    if (shiftAmount < 1 || shiftAmount > 2)
                    {
                        fullLog.Add(string.Format("tDCipher argument shiftAmount for rule 5 is {0} when it should be 1-2. Please report this to thunder725",
                            shiftAmount));
                    }
                    if (directionOfVerticalShift != 'U' && directionOfVerticalShift != 'D')
                    {
                        fullLog.Add(string.Format("tDCipher argument directionOfVerticalShift for rule 5 is {0} when it should be U or D. Please report this to thunder725",
                            directionOfVerticalShift));
                    }

                    // Offset is the opposite of Up/Down since we're encrypting and not decrypting
                    int offset = directionOfVerticalShift == 'U' ? -1 : 1;
                    offset *= shiftAmount * 5;
                    // This Offset is literally an Offset into the table

                    string canvasCopy = currentCanvas;

                    for (int i = 0; i < 15; i ++)
                    {
                        int newIndex = (i + offset + 30) % 15;
                        canvasCopy = canvasCopy.Remove(i, 1).Insert(i, currentCanvas[newIndex].ToString());
                    }

                    // Apply result
                    currentCanvas = canvasCopy;

                    fullLog.Add(startingCanvas);
                    fullLog.Add(string.Format("tShifting every row {0} {1} time{2}, resulting in:",
                        directionOfVerticalShift == 'U' ? "Up": "Down", shiftAmount, shiftAmount == 1 ? "" : "s"));

                    break;
                }


            case 6:
                {
                    string startingCanvas = currentCanvas;

                    // Flip the canvas {direction}
                    // Either vertical or horizontal flip
                    char flipDirection = Convert.ToChar(GetNewDCipherArgument());


                    if (flipDirection == 'H')
                    {
                        string canvasCopy = currentCanvas;

                        for (int i = 0; i < 15; i ++)
                        {
                            // Middle column doesn't move
                            if (i%5 == 2) { continue; }

                            int mirroredIndex = (i / 5)*5 + 4 - (i % 5);
                            canvasCopy = canvasCopy.Remove(mirroredIndex, 1).Insert(mirroredIndex, currentCanvas[i].ToString());
                        }

                        // Apply result
                        currentCanvas = canvasCopy;

                        fullLog.Add(startingCanvas);
                        fullLog.Add("tFlipping the Canvas horizontally to get:");

                    }
                    else if (flipDirection == 'V')
                    {
                        string canvasCopy = currentCanvas;

                        for (int i = 0; i < 15; i++)
                        {
                            // middle row doesn't move
                            if (i / 5 == 1) { continue; }

                            int mirroredIndex = (2 - (i / 5))*5 + (i % 5);
                            canvasCopy = canvasCopy.Remove(mirroredIndex, 1).Insert(mirroredIndex, currentCanvas[i].ToString());
                        }

                        // Apply result
                        currentCanvas = canvasCopy;

                        fullLog.Add(startingCanvas);
                        fullLog.Add("tFlipping the Canvas vertically to get:");
                    }
                    else // VerifyData
                    {
                        fullLog.Add(string.Format("tDCipher argument flipDirection for rule 6 is {0} when it should be H or V. Please report this to thunder725",
                            flipDirection));
                    }

                    break;
                }

            default:
                fullLog.Add("tAsked to use unknown cipher step for D Cipher: " + ruleIndex0To6 + ". Please report this to thunder725");
                break;
        }
    }

    object GetNewDCipherArgument()
    {
        if (currentDCipherArgumentsIndex == -1)
        {
            fullLog.Add("tRan out of Arguments for the DCipher! Please report this to thunder725.");
            return 0;
        }

        object _argument = DCipherArguments[currentDCipherArgumentsIndex];
        fullLog.Add(string.Format("fArgument index {0} is {1}", currentDCipherArgumentsIndex, _argument));

        currentDCipherArgumentsIndex--;
        return _argument;
    }


    void CipherDOuterRotation(char direction, int amount, bool log)
    {
        string startingCanvas = currentCanvas;

        // amount of turning should be 1 to 5
        if (amount < 1 || amount > 5)
        {
            fullLog.Add(string.Format("tDCipher argument AmountOfTurning for rule 0/4 is {0} when it should be 1-5. Please report this to thunder725", amount));
            return;
        }
        // Direction of turning should be exactly C or W
        if (direction != 'C' && direction != 'W')
        {
            fullLog.Add(string.Format("tDCipher argument directionOfTurning for rule 0/4 is {0} when it should be C or W. Please report this to thunder725", direction));
            return;
        }

        // Array giving the index where the light goes if you turn clockwise once
        int[] clockwiseRotationArray = new int[15] { 1, 2, 3, 4, 9, 0, 6, 7, 8, 14, 5, 10, 11, 12, 13 };
        // Ditto for counterclockwise
        int[] counterclockwiseRotationArray = new int[15] { 5, 0, 1, 2, 3, 10, 6, 7, 8, 4, 11, 12, 13, 14, 9 };

        // Determine if we turn clockwise or counter-clockwise
        // HOWEVER!!!
        // Since we are **encrypting**, and the arguments assume decryption, reverse clock and counter
        int[] _rotationArray = direction == 'C' ? counterclockwiseRotationArray : clockwiseRotationArray;


        string _canvasCopy = currentCanvas;
        int _newIndex = 0;

        for (int i = 0; i < 15; i ++)
        {
            // lights at indices 6 7 8 are in the center and don't get rotated
            if (i > 5 && i < 9)
            { continue; }

            // Start at the index of the light
            _newIndex = i;

            for (int j = 0; j < amount; j ++)
            {
                // Rotate it multiple times around
                _newIndex = _rotationArray[_newIndex];
            }

            // Apply in the new canvas the rotated thing
            _canvasCopy = _canvasCopy.Remove(_newIndex, 1).Insert(_newIndex, currentCanvas[i].ToString());
        }

        // Apply result
        currentCanvas = _canvasCopy;

        if (log == false) { return; }
        fullLog.Add(startingCanvas);
        fullLog.Add(string.Format("tCycling the outer twelve lights {0} {1} time{2}, resulting in:",
            direction == 'C' ? "Clockwise" : "Counter-Clockwise", amount, amount == 1 ? "" : "s"));
    }

    void CipherD3x3SquareRotation(int leftmostColumnIndex, int rotationAmount, bool log)
    {
        string startingCanvas = currentCanvas;


        if (rotationAmount != 90 && rotationAmount != 180 && rotationAmount != 270)
        {
            fullLog.Add(string.Format("[Giants Cipher #{0}] DCipher argument RotationAmount for rule 3 is {1} when it should be 90/180/270. Please report this to thunder725",
                moduleId, rotationAmount));
        }
        if (leftmostColumnIndex < 0 || leftmostColumnIndex > 2)
        {
            fullLog.Add(string.Format("[Giants Cipher #{0}] DCipher argument LeftmostColumnIndex for rule 3 is {1} when it should be 0-2. Please report this to thunder725",
                moduleId, leftmostColumnIndex));
        }

        // Array with offsets for the squares, to add to the topleft corner
        int[] indexOffsetArray = new int[8] { 0, 1, 2, 5, 7, 10, 11, 12 };

        // This is how to rotate once counterclockwise (since we're encrypting)
        // Read the index of the current light in the square (0-8 reading order) and it gives you a new indew (0-8)
        int[] counterClockRotationArray = new int[8] { 5, 3, 0, 6, 1, 7, 4, 2};

        // Rotation Amount is 1 2 3
        rotationAmount = rotationAmount / 90;

        string _canvasCopy = currentCanvas;

        // For each light in the 3x3 square (index 0-7 in reading order)
        // 0 1 2
        // 3 . 4
        // 5 6 7
        // Read into the counterClockRotationArray 1 to 3 times to rotate
        // Then use that final value (with the starting index) using the indexOffsetArray
        // to determine which light goes where
        for (int i = 0; i < 8; i++)
        {
            // Start at the index of the light
            int _newIndex = i;

            for (int j = 0; j < rotationAmount; j++)
            {
                // Rotate it multiple times around
                _newIndex = counterClockRotationArray[_newIndex];
            }

            _newIndex = leftmostColumnIndex + indexOffsetArray[_newIndex];
            int _oldIndex = leftmostColumnIndex + indexOffsetArray[i];

            // Apply in the new canvas the rotated thing
            _canvasCopy = _canvasCopy.Remove(_newIndex, 1).Insert(_newIndex, currentCanvas[_oldIndex].ToString());
        }

        // Apply result
        currentCanvas = _canvasCopy;

        if (log == false) { return; }
        fullLog.Add(startingCanvas);
        fullLog.Add(string.Format("tCycling the 3x3 square in columns {0} for {1}°, resulting in:",
            leftmostColumnIndex == 0 ? "ABC" : leftmostColumnIndex == 1 ? "BCD" : "CDE", rotationAmount * 90));
    }



    void FlipLightInCanvas(int lightIndex)
    {
        char _value = currentCanvas[lightIndex];
        _value = _value == '.' ? 'X' : '.';

        currentCanvas = currentCanvas.Remove(lightIndex, 1).Insert(lightIndex, _value.ToString());

    }

    void PrintCanvasToLog(bool useInternalCanvas = true, string CanvasToPrint = "")
	{
        if (useInternalCanvas)
        {
            CanvasToPrint = currentCanvas;
        }
        ModuleLog(true, CanvasToPrint.Substring(0, 5));
        ModuleLog(true, CanvasToPrint.Substring(5, 5));
        ModuleLog(true, CanvasToPrint.Substring(10, 5));
    }




    public void ModuleLog(bool LogInLfa, string message, params object[] args)
    {
        if (LogInLfa)
        { Debug.LogFormat("[Giants Cipher #{0}] {1}", moduleId, string.Format(message, args)); }
        else
        { Debug.LogFormat("<Giants Cipher #{0}> {1}", moduleId, string.Format(message, args)); }
    }


    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //  Twitch Plays
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"“!{0} Press 1” to press the first Pattern shown in the manual in reading order. Valid numbers are 1-5.";
#pragma warning restore 414

    KMSelectable[] ProcessTwitchCommand(string command)
    {
        // Credit to Royal_Flu$h for this line 
        var commandParts = command.ToLowerInvariant().Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        // We only accept submissions with press and one number
        if (commandParts.Length != 2)
        {
            return null;
        }

        if (!commandParts[0].Equals("press"))
        {
            return null;
        }

        int _pressedIndex = int.Parse(commandParts[1]);

        if (_pressedIndex < 1 || _pressedIndex > 5)
        {
            return null;
        }

        // -1 since we accept 1-5 but the indices are 0-4
        return new KMSelectable[] { pressableButtons[_pressedIndex - 1] };
    }

    // Auto-solve if Twitch Plays needs to force a solve
    KMSelectable[] TwitchHandleForcedSolve()
    {
        moduleSolved = true;

        return new KMSelectable[] { pressableButtons[resultID] };
    }

}
