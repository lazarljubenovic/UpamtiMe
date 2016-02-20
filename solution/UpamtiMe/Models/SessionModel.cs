﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Web;
using Data;
using Data.DTOs;

namespace UpamtiMe.Models
{
    public class SessionModel
    {
        public List<CardSessionDTO> Cards { get; set; }
        public int CourseID { get; set; }
        public string Link { get; set; }

        public static SessionModel LoadLearningSession(int userID, int courseID, int? levelID, int? numberOfCards)
        {
            DataClasses1DataContext dc = new DataClasses1DataContext();

            //ovo je zapravo defaultna vrenost, mora ovako jer inace mora da bude compile-time constant
            if (numberOfCards == null)
                numberOfCards = ConfigurationParameters.LearningSessionCardNumber;

            if (levelID == null)
            {
                levelID = (from c in dc.Cards
                    from l in dc.Levels
                    where
                       l.courseID == courseID && c.levelID == l.levelID &&
                        !dc.UsersCards.Any(a => a.cardID == c.cardID && a.userID == userID)
                           select new {id = l.levelID, no = l.number}).OrderBy(a => a.no).First().id;
            }

            SessionModel sm = new SessionModel();
            sm.CourseID = courseID;
            sm.Cards = (from c in dc.Cards
                where
                    c.levelID == levelID.Value &&
                    !dc.UsersCards.Any(a => a.cardID == c.cardID && a.userID == userID)
                select new CardSessionDTO
                {
                    BasicInfo = new CardBasicDTO
                    {
                        CardID = c.cardID,
                        Question = c.question,
                        Answer = c.answer,
                        Description = c.description,
                        Image = c.image == null ? null : c.image.ToArray(),
                        Number = c.number,
                    },
                    CardChallange = new CardChallangeDTO
                    {
                        MultipleChoice = new List<string>(),
                        Hangman = "",
                        Scrabble = new List<string>(),
                        Challenges = ConfigurationParameters.ChallengesLearn,
                    }
                }).OrderBy(a => a.BasicInfo.Number).ToList();

            ///// kreiranje multiplechoica za svaku karticu sesije /////
            double otherCardAnswersChance = 0.5; // ovo znaci da u 50% slucajeva hocemo odgovore drugih kartica za multiple choice, a ne tumbanje
            foreach (CardSessionDTO csd in sm.Cards.Take(numberOfCards.Value))
            {
                csd.CardChallange.MultipleChoice = new List<string>();
                csd.CardChallange.Hangman = csd.BasicInfo.Answer;
                csd.CardChallange.Scrabble = Regex.Split(csd.BasicInfo.Answer, string.Empty).ToList();

                Random rnd = new Random();

                // uzimamo random odgovore iz lekcije ako je otherCardAnswersChance ili ako je rec mnogo kratka pa ne mogu da se tumbaju slova
                if (rnd.NextDouble() <= otherCardAnswersChance)
                {
                    // ako uopste ima dovoljno u lekciji, onda uzimamo 
                    if (sm.Cards.Count >= 4)
                    {
                        csd.CardChallange.MultipleChoice = getMultipleChoiceAnswers(sm.Cards, csd);
                    }
                    else
                    {   // pokusamo da istumbamo ako nema da se uzme iz lekcije
                        if (csd.BasicInfo.Answer.Split(' ').OrderByDescending(k => k.Length).First().Length > 4)
                            csd.CardChallange.MultipleChoice = generateWrongAnswers(csd.BasicInfo.Answer);
                        else
                            csd.CardChallange.Challenges.Replace("multiple;", "");
                    }
                }
                else  // ako je 1 - otherCardAnswersChance i ako ima dovoljno dugacka rec za tumbanje
                {
                    if (csd.BasicInfo.Answer.Split(' ').OrderByDescending(k => k.Length).First().Length > 4)
                        csd.CardChallange.MultipleChoice = generateWrongAnswers(csd.BasicInfo.Answer);
                    else
                    {
                        // ako uopste ima dovoljno u lekciji, onda uzimamo 
                        if (sm.Cards.Count >= 4)
                        {
                            csd.CardChallange.MultipleChoice = getMultipleChoiceAnswers(sm.Cards, csd);
                        }
                        else
                        {
                            csd.CardChallange.Challenges.Replace("multiple;", "");
                        }
                    }
                }
            }

            ///// kreiranje hangmana za svaku karticu sesije /////
            foreach (CardSessionDTO csd in sm.Cards.Take(numberOfCards.Value))
            {
                // pravi hint za hangmana za svaku karticu
                // drugi parametar je koliki deo reci hocemo da mu prikazemo, stavio sam 20% zasad
                csd.CardChallange.Hangman = HangmanHints(csd.BasicInfo.Answer, 0.2);
            }

            ///// kreiranje scrabble-a za svaku karticu sesije /////
            foreach (CardSessionDTO csd in sm.Cards.Take(numberOfCards.Value))
            {
                csd.CardChallange.Scrabble = scrabbleCharacters(csd.BasicInfo.Answer, 0.2, sm.Cards);
            }

            sm.Cards = sm.Cards.Take(numberOfCards.Value).ToList();
            return sm;
        }

        public static List<string> getMultipleChoiceAnswers(List<CardSessionDTO> lekcijaCards, CardSessionDTO correctCard)
        {
            Random rnd = new Random();
            List<CardSessionDTO> cardsCopy = new List<CardSessionDTO>(lekcijaCards);
            cardsCopy.Remove(correctCard);
            List<CardSessionDTO> temp = cardsCopy.OrderBy(item => rnd.Next()).Take(3).ToList();
            List<string> answers = new List<string>();
            answers.Add(correctCard.BasicInfo.Answer);
            foreach (CardSessionDTO t in temp)
            {
                answers.Add(t.BasicInfo.Answer);
            }

            return answers;
        }

        public static List<string> scrabbleCharacters(string answer, double hardness, List<CardSessionDTO> allCards)
        {
            char[] characters = answer.ToCharArray();
            List<string> scrabbleChars = new List<string>();
            foreach (char c in characters)
            {
               scrabbleChars.Add(c.ToString());
            }

            Random rnd = new Random();
            int additionalChars = Convert.ToInt32(Math.Ceiling(answer.Length * hardness));
            
            for (int i = 0; i < additionalChars; i++)
            {
                int wordIndex = rnd.Next(0, allCards.Count);
                int charIndex = rnd.Next(0, allCards[wordIndex].BasicInfo.Answer.Length);
                scrabbleChars.Add(allCards[wordIndex].BasicInfo.Answer[charIndex].ToString());
            }

            return scrabbleChars;
        }

        public static List<string> generateWrongAnswers(string correct)
        {
            string[] words = correct.Split(' ');
            string longest = words.OrderByDescending(s => s.Length).First();
            int indexLongest = Array.FindIndex(words, temp => temp == longest); // nalazi indeks najduze reci
            int numOfCandidates = 0; // koliko reci je kandida thttp://prntscr.com/9pr8hiza neko cackanje
            List<int> candidateIndexes = new List<int>(); // indeksi kandidata za zamenu (indeksi u nizu words)
            int minimumLength = 5; // samo reci od ovoliko SLOVA ili vise su kandidati za zamenu
            List<string> finalArray = new List<string>(); // krajnji niz koji ce da sadrzi correct + 3 wrong odgovora
            finalArray.Add(correct);

            // prvo da se prebroji koliko ima reci duzih od minimumLength, tj reci koje su kandidati za modifikaciju
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length < minimumLength)
                    continue;
                if (words[i].Length == minimumLength && (words[i][minimumLength - 1] == '.' || words[i][minimumLength - 1] == ',' || words[i][minimumLength - 1] == '!' || words[i][minimumLength - 1] == '?'))
                    continue;
                numOfCandidates++; // ako je rec duzine minimumLength SLOVA (ne bilo kojih karaktera) ili duza, kandidat je za zamenu
                candidateIndexes.Add(i); // index reci koja je kandidat za zamenu u nizu svih reci odgovora
            }

            Random rnd = new Random();

            // ako se nisu uzeli odgovori iz drugih kartica nego je doslo do mesanja
            // a nema nijednog kandidata za mesanje slova, onda se vracaju tacan i 3 prazna
            // (ovo bi trebalo da se desi MNOGO retko ili nikad)
            if (numOfCandidates == 0)
                return new List<string>() { correct, "", "", "" }.OrderBy(item => rnd.Next()).ToList();

            for (var i = 0; i < 3; i++) // treba da napravimo 3 pogresna odgovora
            {
                // najcesce ce da bude samo jedna rec kao kandidat za izmenu, eventualno dve
                // ako je kandidat samo 1 rec, 100% se menja ona i samo ona
                // ako imamo dva kandidata, onda se 80% menja jedna samo, a 20% da se promene obe reci
                // ako imamo tri kandidata, onda se 72% menja jedna samo, a 28% da se promene dve reci
                // ako imamo cetiri kandidata, onda se 65% menja jedna samo, a 35% da se promene dve reci
                int modificationsNum = Convert.ToInt32(Math.Floor(rnd.NextDouble() * (1.0 + (double)numOfCandidates * 0.13) + 1.0));

                // ako je 1 kandidat moze da ovo gore zabrlja pa ovime ispravljam
                if (numOfCandidates == 1)
                    modificationsNum = 1;

                string[] newAnswer = correct.Split(' '); // niz reci koji cemo da koristimo za pravljenje novog odgovora
                List<int> modifiedWordsIndexes = new List<int>(); // indeksi reci koje su izmenjene
                int indexWordToModify; // indeks reci koju modifikujemo trenutno
                int modificationType; // nacin na koji ce da se modifikuje rec
                                        // 1 - dupliciraj neko slovo
                                        // 2 - ukloni slovo
                                        // 3 - zameni dva slova

                int currentCandidateNum = numOfCandidates; // pomocna
                List<int> currentCandidates = new List<int>(); // indeksi TRENUTNE reci koje su kandidati za zamenu
                                                                // kopiramo indekse kandidata u jos jedan niz, da imamo sa njim da radimo
                for (var z = 0; z < numOfCandidates; z++)
                    currentCandidates.Add(candidateIndexes[z]);

                string newAnswerString = null; // string gde ce da pamtimo rec nakon sto je modifikujemo
                                                // odavde pocinjemo da generisemo odgovor
                for (var k = 0; k < modificationsNum; k++)
                {
                    if (modificationsNum == 1) // ako treba samo jednu rec da izmenimo, onda cemo najduzu
                    {
                        indexWordToModify = indexLongest;
                    }
                    else // ako treba vise njih, onda sa vecom verovatnocom najduzu i sa manjim verovatnocama neku drugu
                    {
                        int randValue = rnd.Next(1, 11); // vraca broj od 1 do 10
                        if (randValue <= 8 && Array.Exists(modifiedWordsIndexes.ToArray(), temp => temp == indexLongest)) // ovaj drugi uslov je da vidi da nije najduza rec mozda vec izmenjena
                        {
                            indexWordToModify = indexLongest; // 80% da izmenimo najduzu rec
                        }
                        else
                        {
                            do
                            {
                                int tmp = rnd.Next(0, currentCandidateNum);
                                indexWordToModify = currentCandidates[tmp];
                            } while (modifiedWordsIndexes.IndexOf(indexWordToModify) != -1);
                        }
                    }
                    currentCandidateNum--; // izabrali smo neku rec za zamenu, znaci smanjujemo broj kandidata
                    currentCandidates.RemoveAt(currentCandidates.IndexOf(indexWordToModify)); // i odabranu rec za menjanje sad sklanjamo iz liste
                    modifiedWordsIndexes.Add(indexWordToModify); // i stavljamo je u listu modifikovanih (indeks njen)
                    //lazar: linija gore: stack overflow


                    int charIndex = rnd.Next(0, newAnswer[indexWordToModify].Length - 1); //index karaktera oko kog cemo neku izmenu da napravimo
                    modificationType = rnd.Next(1, 4);
                    switch (modificationType)
                    {
                        case 1: // duplira neko slovo (izbegavajuci prvi i poslednji karakter)
                            {
                                newAnswer[indexWordToModify] = newAnswer[indexWordToModify].Substring(0, charIndex) + newAnswer[indexWordToModify][charIndex] + newAnswer[indexWordToModify].Substring(charIndex);
                                newAnswerString = String.Join(" ", newAnswer);
                                break;
                            }
                        case 2: // ukloni neko slovo (izbegavajuci prvi i poslednji karakter)
                            {
                                newAnswer[indexWordToModify] = newAnswer[indexWordToModify].Substring(0, charIndex) + newAnswer[indexWordToModify].Substring(charIndex + 1);
                                newAnswerString = String.Join(" ", newAnswer);
                                break;
                            }
                        case 3: // zameni dva slova, charindex i charindex+1
                            {
                                charIndex -= 1;
                                if (charIndex == 0) // ipak da ne menjamo nulti karakter sa prvim...
                                    charIndex = 1;
                                newAnswer[indexWordToModify] = newAnswer[indexWordToModify].Substring(0, charIndex) + newAnswer[indexWordToModify][charIndex + 1] + newAnswer[indexWordToModify][charIndex] + newAnswer[indexWordToModify].Substring(charIndex + 2);
                                // lazar: linija gore: argument out of range
                                newAnswerString = String.Join(" ", newAnswer);
                                break;
                            }
                    }
                }
                finalArray.Add(newAnswerString);
            }

            // krajnja provera da nema dva tacna odgovora
            int correctCount = 0;
            for (int i = 0; i < finalArray.Count; i++)
            {
                if (correct == finalArray[i])
                    correctCount++;
            }
            if (correctCount != 1)
                return generateWrongAnswers(correct); // pozovi opet funkciju, ovo bi trebalo mnogo retko, skoro nikad
                //lazar: puklo ovde, stack overflow
            else
            {
                finalArray = finalArray.OrderBy(item => rnd.Next()).ToList(); // shuffle
                return finalArray; // vraca niz od 4 odgovora sa izmesanim redosledima
            }
        }

        public static string HangmanHints(string answer, double showpercent)
        {
            string answerHint = answer;
            char hiddenChar = '-';
            char spaceChar = ' ';
            char[] unobservedChars = { ' ', ',', '.', '!', '?' };
            List<int> hideCandidates = new List<int>();
            int hideCharCount;

            Random rnd = new Random();

            int charNum = answer.Length;

            // smanjujemo charNum za svaki razmak, zarez i to tako jer nisu bitni ovde
            // i ujedno u pomocni niz upisujemo indekse svih slova (ne karaktera)
            for (int i = 0; i < answer.Length; i++)
                if (unobservedChars.Contains(answer[i]))
                    charNum--;
                else
                    hideCandidates.Add(i);

            // izracunamo broj slova koje cemo da prekrijemo, tipa ako u reci imamo
            // 10 slova (ne karaktera), a 37% hocemo da prikazemo, prekricemo 6 od 10 slova
            // a ako imamo samo 1 slovo kandidat za skrivanje, sakricemo ga sigurno
            if (charNum == 1)
                hideCharCount = 0;
            else
                hideCharCount = charNum - Convert.ToInt32(Math.Round(charNum * showpercent));

            if (hideCharCount == charNum) // ako se palo da ih sakrijemo sve, ipak cemo 1 da ostavimo slobodnim
                hideCharCount -= 1;

            for (int i = 0; i < hideCharCount; i++)
            {
                int hideCharIndex = rnd.Next(0, hideCandidates.Count);

                System.Text.StringBuilder sb = new System.Text.StringBuilder(answerHint);
                sb[hideCandidates[hideCharIndex]] = hiddenChar;
                answerHint = sb.ToString();
                hideCandidates.RemoveAt(hideCharIndex);
            }

            answerHint = answerHint.Replace(' ', spaceChar);

            return answerHint;
        }

        public static SessionModel LoadReviewSession(int userID, int courseID, int? levelID, int? numberOfCards)
        {
            DataClasses1DataContext dc = new DataClasses1DataContext();

            if (numberOfCards == null)
                numberOfCards = ConfigurationParameters.ReviewSessionCardNumber;


            SessionModel sm = new SessionModel();
            sm.CourseID = courseID;

            sm.Cards = (from c in dc.Cards
                        from l in dc.Levels
                        from u in dc.UsersCards
                        where u.userID == userID && u.cardID == c.cardID && ((levelID != null && c.levelID == levelID ) || (levelID == null && c.levelID == l.levelID)) && u.ignore == false && u.nextSee < DateTime.Now && (levelID != null || l.courseID == courseID)
                        select new CardSessionDTO
                        {
                            UserCardInfo = new CardUserDTO()
                            {
                                Combo = u.cardCombo,
                                CorrectAnswers = u.correctAnswers,
                                WrongAnswers = u.wrongAnswers,
                                LastSeen = u.lastSeen,
                                SinceSeen = Convert.ToInt32(DateTime.Now.Subtract(u.lastSeen).TotalMinutes),
                                NextSee = u.nextSee,
                                SincePlan = Convert.ToInt32(DateTime.Now.Subtract(u.nextSee).TotalMinutes),
                                UserCardID = u.usersCardID,
                            },
                            BasicInfo = new CardBasicDTO
                            {
                                Question = c.question,
                                Answer = c.answer,
                                Description = c.description,
                                Image = c.image == null ? null : c.image.ToArray(),
                                Number = c.number,
                            },
                            CardChallange = new CardChallangeDTO()
                            {
                                MultipleChoice = new List<string>(new string[] { c.answer, c.answer, c.answer, c.answer }),
                                Hangman = c.answer,
                                Scrabble = Regex.Split(c.answer, string.Empty).ToList(),
                                Challenges = (u.goodness > 0.6 ? "" : "mutiple;")  +  ConfigurationParameters.ChallengesReview 
                            }
                        }).OrderBy(a => a.UserCardInfo.NextSee).Take(numberOfCards.Value).ToList();
            
            return sm;
        }

        
        public static SessionModel LoadLinkySession(int userID, int courseID, int? levelID, int? numberOfCards)
        {
            DataClasses1DataContext dc = new DataClasses1DataContext();

            if (numberOfCards == null)
                numberOfCards = ConfigurationParameters.LinkySessionCardNumber;

            SessionModel sm = new SessionModel();
            sm.CourseID = courseID;
            if (levelID != null)
            {
                sm.Cards = (from c in dc.Cards
                            from u in dc.UsersCards
                            where u.userID == userID &&  u.cardID == c.cardID && c.levelID == levelID && u.ignore == false && u.nextSee > DateTime.Now 
                            select new CardSessionDTO
                            {
                                BasicInfo = new CardBasicDTO
                                {
                                    Question = c.question,
                                    Answer = c.answer,
                                    Description = c.description
                                }
                            }).Take(numberOfCards.Value).ToList();
            }
            else
            {
                sm.Cards = (from c in dc.Cards
                            from l in dc.Levels
                            from u in dc.UsersCards
                            where u.userID == userID && u.cardID == c.cardID && c.levelID == l.levelID && l.courseID == courseID && u.ignore == false && u.nextSee > DateTime.Now 
                            select new CardSessionDTO
                            {
                                BasicInfo = new CardBasicDTO
                                {
                                    Question = c.question,
                                    Answer = c.answer,
                                    Description = c.description
                                }
                            }).Take(numberOfCards.Value).ToList();
            }

            sm.Cards = sm.Cards.GroupBy(a => a.BasicInfo.Answer).Select(a => a.First()).ToList();

            return sm;
        }
    }
}