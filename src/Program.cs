using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net;
using Ical.Net.Serialization;

namespace cleaningcal
{
    public class Chore
    {
        public string Room { get; set; }
        public string Item { get; set; }
        public string Fixed { get; set; }
        public int Freq { get; set; }
        public string Details { get; set; }
    }

    public class AssignableChore
    {
        public Chore Chore { get; set; }
        public DateTime AssignedStartDate { get; set; }
        public TimeSpan Repetition { get; set; }
        public TimeSpan Freedom { get; set; }
        public string FixedPerson { get; set; }
    }

    public class AssignedChore
    {
        public Chore Chore { get; set; }
        public DateTime TimeStamp { get; set; }
        public Person AssignedTo { get; set; }
        public TimeSpan Freedom { get; set; }
    }

    public class Person
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var configPath = "/Users/philippegerber/cleaningcal/ressources/chores.json";
            var personsPath = "/Users/philippegerber/cleaningcal/ressources/persons.json";
            var calendarPath = "/Users/philippegerber/cleaningcal/ressources/chores_::NAME::.ics";

            var chores = JsonConvert.DeserializeObject<List<Chore>>(File.ReadAllText(configPath));
            var persons = JsonConvert.DeserializeObject<List<Person>>(File.ReadAllText(personsPath));

            var startDate = new DateTime(2021, 1, 18);
            var lastDate = new DateTime(2021, 12, 31);

            var assignableChores = GetAssignableChores(chores, startDate);
            var choresPerFreq = assignableChores.GroupBy(c => c.Repetition);
            var assignedsChores = GetAssignedChores(startDate, lastDate, persons, choresPerFreq);

            foreach(var gr in assignedsChores.GroupBy(c => c.AssignedTo))
            {
                var personName = gr.Key.Name;

                var calendar = new Calendar();
                foreach (var chore in gr)
                {
                    var e = GetChoreEvent(chore);
                    calendar.Events.Add(e);
                }

                var serializer = new CalendarSerializer();
                var serializedCalendar = serializer.SerializeToString(calendar);

                var outputPath = calendarPath.Replace("::NAME::", personName);
                File.WriteAllText(outputPath, serializedCalendar);
            }
        }

        private static CalendarEvent GetChoreEvent(AssignedChore chore)
        {
            var reminder = new Alarm
            {
                Summary = chore.Chore.Item,
                Action = AlarmAction.Display,
                Trigger = new Trigger(TimeSpan.FromMinutes(0))
            };

            var reminderEmail = new Alarm
            {
                Summary = chore.Chore.Item,
                Action = AlarmAction.Email,
                Trigger = new Trigger(TimeSpan.FromMinutes(0))
            };

            var attendee =
                    new Attendee()
                    {
                        Value = new Uri($"mailto:{chore.AssignedTo.Email}"),
                        CommonName = chore.AssignedTo.Name
                    };

            var e = new CalendarEvent()
            {
                Start = new CalDateTime(chore.TimeStamp.AddHours(8)),
                End = new CalDateTime(chore.TimeStamp.AddHours(8).Add(chore.Freedom)),
                Summary = $"{chore.Chore.Item} - {chore.AssignedTo.Name}",
                Location = chore.Chore.Room,
                Attendees = { attendee },
                Alarms = { reminder, reminderEmail }
            };

            return e;
        }

        private static List<AssignedChore> GetAssignedChores(DateTime startDate, DateTime lastDate, List<Person> persons, IEnumerable<IGrouping<TimeSpan, AssignableChore>> choresPerFreq)
        {
            var assigneds = new List<AssignedChore>();
            foreach (var gr in choresPerFreq)
            {
                var pCount = 0;
                foreach (var chore in gr.ToList())
                {
                    var startPerson = persons[pCount];

                    var nbOccurence =
                            (lastDate - chore.AssignedStartDate).TotalDays / chore.Repetition.TotalDays;

                    var assignedChores =
                            Enumerable.Range(0, (int)nbOccurence + 1)
                                .Select(
                                    r => new AssignedChore
                                    {
                                        Chore = chore.Chore,
                                        TimeStamp = chore.AssignedStartDate.Add(r * chore.Repetition),
                                        AssignedTo =
                                            string.IsNullOrEmpty(chore.FixedPerson)
                                            ? persons[(pCount + r) % persons.Count()]
                                            : persons.Where(c => c.Name == chore.FixedPerson).Single(),
                                        Freedom = chore.Freedom
                                    })
                                .ToList();

                    assigneds.AddRange(assignedChores);

                    pCount = +1;
                    if (pCount > persons.Count())
                        pCount = 0;
                }
            }

            return assigneds;
        }

        private static List<AssignableChore> GetAssignableChores(List<Chore> chores, DateTime startDate)
        {
            var assignableChores =
                    chores
                        .Where(c => c.Freq == 1)
                        .Select(c => new AssignableChore()
                        {
                            Chore = c,
                            AssignedStartDate = startDate,
                            Repetition = TimeSpan.FromDays(7),
                            Freedom = TimeSpan.FromHours(1)
                        })
                        .ToList();

            foreach (var chore in chores.Where(c => c.Freq > 1))
            {
                var randomDayStart = new Random().Next(chore.Freq + 1);
                var assignedFirstDate = startDate.AddDays(randomDayStart);

                var freedomDays =
                        chore.Freq > 10
                        ? (int)(0.1 * chore.Freq)
                        : 1;

                var assignableChore =
                        new AssignableChore()
                        {
                            Chore = chore,
                            AssignedStartDate = assignedFirstDate,
                            Repetition = TimeSpan.FromDays(chore.Freq),
                            Freedom = TimeSpan.FromDays(freedomDays),
                            FixedPerson = chore.Fixed
                        };
                assignableChores.Add(assignableChore);
            }

            return assignableChores;
        }
    }
}
