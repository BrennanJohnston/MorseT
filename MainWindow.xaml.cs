using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MorseT {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        static String settingsFilename = "settings.cfg";
        static String dictionaryFilename = "morse_dictionary.txt";
        static String morseBeepAudioFilenameDefault = "beep1.wav";
        static bool audioFileLoaded = false;
        static String morseBeepAudioFilename = morseBeepAudioFilenameDefault;
        static SoundPlayer audioPlayer;
        static Key inputKey = Key.Space; // read config file and load this in later
        static long dashTime = 100; // time key is held down until input is interpreted as a dash, anything less is a dot
        static long characterAcceptTime = 200; // time of no input before character is accepted in milliseconds
        static long wordAcceptTime = 250; // time of no input and after last characterAcceptTime in milliseconds
        static Dictionary<String, String> morseDictionary = new Dictionary<String, String>();
        static bool inputKeyPressed = false;
        static Thread morseThread;
        static bool running = true;

        public MainWindow() {
            InitializeComponent();
            clearButton.Focusable = false;
            wordTextOutput.Text = "_";
            morseTextOutput.Text = "_";

            loadSettings(settingsFilename);
            setAudioFile(morseBeepAudioFilename);
            fillDictionary();
            Console.WriteLine("wordAcceptTime = " + wordAcceptTime);

            morseThread = new Thread(() => {
                while (running) {
                    String word = getMorseWord();
                    if (word.Length > 0) {
                        try {
                            Dispatcher.Invoke(() => {
                                wordTextOutput.Text = wordTextOutput.Text.Replace("_", " _");
                            });
                        } catch (TaskCanceledException tce) {
                            Console.WriteLine(tce);
                        }
                    }
                }
            });

            morseThread.SetApartmentState(ApartmentState.STA);
            morseThread.Start();
        }

        String getMorseWord() {
            String word = "";
            bool gettingWord = true;
            long timeOfRelease = getTimeMilliseconds();
            while(gettingWord) {
                if(Keyboard.IsKeyDown(inputKey)) {
                    String letter = getMorseLetter();
                    word += letter;
                    try {
                        Dispatcher.Invoke(() => {
                            wordTextOutput.Text = wordTextOutput.Text + "_";
                            morseTextOutput.Text = morseTextOutput.Text.Replace("_", " _");
                            
                        });
                    } catch (TaskCanceledException tce) {
                        Console.WriteLine(tce);
                    }
                    timeOfRelease = getTimeMilliseconds();
                }

                if(getTimeMilliseconds() - timeOfRelease > wordAcceptTime) {
                    gettingWord = false;
                }
            }

            return word;
        }

        String getMorseLetter() {
            String letter = "[?]";
            String morse = "";
            bool gettingLetter = true;
            bool morseInputValid = true; // false if morse dictionary returns null
            long timeOfPress = getTimeMilliseconds();
            long timeOfRelease = timeOfPress;

            while (gettingLetter) {
                while(Keyboard.IsKeyUp(inputKey)) {
                    if(getTimeMilliseconds() - timeOfRelease > characterAcceptTime) {
                        gettingLetter = false;
                        break;
                    }
                    Thread.Sleep(1);
                }

                timeOfPress = getTimeMilliseconds();
                //if (audioFileLoaded)
                //    audioPlayer.PlayLooping();

                // wait for key release
                while (Keyboard.IsKeyDown(inputKey)) {
                    Thread.Sleep(1);
                }

                //if(audioFileLoaded)
                //    audioPlayer.Stop();

                if(Keyboard.IsKeyUp(inputKey) && gettingLetter) {
                    // key released
                    timeOfRelease = getTimeMilliseconds();
                    long keyHoldTime = timeOfRelease - timeOfPress;
                    String morseChar = "";
                    morseChar = keyHoldTime < dashTime ? "." : "-";
                    morse += morseChar;

                    try {
                        letter = morseDictionary[morse];
                        if(letter != null && !morseInputValid) {
                            Dispatcher.Invoke(() => {
                                if(wordTextOutput.Text.Length > 2) {
                                    wordTextOutput.Text = wordTextOutput.Text.Substring(0, wordTextOutput.Text.Length - 3) + letter;
                                }
                            });
                            morseInputValid = true;
                        }
                        
                    } catch (KeyNotFoundException knfe) {
                        letter = "[?]";
                    }

                    try {
                        Dispatcher.Invoke(() => {
                            if(wordTextOutput.Text.Length > 0) {
                                if (letter == "[?]" && morseInputValid) {
                                    morseInputValid = false;
                                    wordTextOutput.Text = wordTextOutput.Text.Substring(0, wordTextOutput.Text.Length - 1) + letter;
                                } else if(morseInputValid) {
                                    wordTextOutput.Text = wordTextOutput.Text.Substring(0, wordTextOutput.Text.Length - 1) + letter;
                                }
                            }

                            morseTextOutput.Text = morseTextOutput.Text.Replace("_", morseChar + "_");
                        });
                    } catch (TaskCanceledException tce) {
                        Console.WriteLine(tce);
                    }
                }
            }

            try {
                letter = morseDictionary[morse];
            } catch (KeyNotFoundException knfe) {
                letter = "[?]";
            }

            return letter;
        }

        static void setAudioFile(String filename) {
            if(audioPlayer != null) audioPlayer.Dispose();

            morseBeepAudioFilename = filename;

            bool fileExists = File.Exists(morseBeepAudioFilename);
            if (!fileExists) morseBeepAudioFilename = morseBeepAudioFilenameDefault;
            fileExists = File.Exists(morseBeepAudioFilename);

            if (fileExists) {
                try {
                    audioPlayer = new SoundPlayer(morseBeepAudioFilename);
                    audioFileLoaded = true;
                } catch (UriFormatException ufe) {
                    audioFileLoaded = false;
                }
            } else {
                audioFileLoaded = false;
            }
        }

        static void fillDictionary() {
            StreamReader file;
            try {
                file = new StreamReader(dictionaryFilename);
                String line;
                String[] lineSplit;
                while ((line = file.ReadLine()) != null) {
                    lineSplit = line.Split(' ');
                    if (lineSplit.Length > 1) {
                        try {
                            morseDictionary.Add(lineSplit[0], lineSplit[1]);
                        } catch (ArgumentNullException ane) {
                            Console.WriteLine("ArgumentNullException: An entry in the dictionary file was invalid.  Continuing.");
                        } catch (ArgumentException ae) {
                            Console.WriteLine("ArgumentException: An entry in the dictionary file was invalid.  Continuing.");
                        }
                    }
                }

                file.Close();
            } catch (FileNotFoundException fnfe) {
                Console.WriteLine("fillDictionary failed:");
                Console.WriteLine(fnfe.ToString());
            } catch (IOException ioe) {
                Console.WriteLine("fillDictionary failed:");
                Console.WriteLine(ioe.ToString());
            }
        }

        static void loadSettings(String filename) {
            StreamReader file;
            try {
                file = new StreamReader(filename);
                String line;
                String[] lineSplit;
                while ((line = file.ReadLine()) != null) {
                    lineSplit = line.Split(' ');
                    if (lineSplit.Length > 2) {
                        switch (lineSplit[0]) {
                            case "morseBeepAudioFilename":
                                morseBeepAudioFilename = lineSplit[2];
                                break;
                            case "dictionaryFilename":
                                dictionaryFilename = lineSplit[2];
                                break;
                            case "dashTime":
                                try {
                                    dashTime = long.Parse(lineSplit[2]);
                                } catch (ArgumentNullException ane) {
                                    Console.WriteLine(ane);
                                } catch (FormatException fe) {
                                    Console.WriteLine(fe);
                                }

                                break;
                            case "characterAcceptTime":
                                try {
                                    characterAcceptTime = long.Parse(lineSplit[2]);
                                } catch (ArgumentNullException ane) {
                                    Console.WriteLine(ane);
                                } catch (FormatException fe) {
                                    Console.WriteLine(fe);
                                }

                                break;
                            case "wordAcceptTime":
                                try {
                                    wordAcceptTime = long.Parse(lineSplit[2]);
                                } catch (ArgumentNullException ane) {
                                    Console.WriteLine(ane);
                                } catch (FormatException fe) {
                                    Console.WriteLine(fe);
                                }

                                break;
                        }
                    }
                }
            } catch (FileNotFoundException fnfe) {
                Console.WriteLine("loadSettings failed:");
                Console.WriteLine(fnfe.ToString());
            } catch (IOException ioe) {
                Console.WriteLine("loadSettings failed:");
                Console.WriteLine(ioe.ToString());
            }
        }

        static long getTimeMilliseconds() {
            return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        // ===================================
        // Event Handlers
        // ===================================

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            running = false;
            if(audioPlayer != null) audioPlayer.Dispose();
        }

        private void clearButton_Click(object sender, RoutedEventArgs e) {
            wordTextOutput.Text = "_";
            morseTextOutput.Text = "_";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) {
            if (audioFileLoaded)
                audioPlayer.PlayLooping();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e) {
            if (audioFileLoaded)
                audioPlayer.Stop();
        }
    }
}
