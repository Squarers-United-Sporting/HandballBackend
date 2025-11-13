// Install required NuGet packages:
// dotnet add package Twilio.AspNet.Core
// dotnet add package Microsoft.AspNetCore.Mvc

using HandballBackend.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Twilio.AspNet.Core;
using Twilio.TwiML;

namespace HandballBackend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SmsController(HandballContext db) : TwilioController {
    [HttpPost]
    public TwiMLResult ReceiveTextMessage() {
        // Read incoming SMS data
        var from = ((string) Request.Form["From"]!).Trim();
        var body = ((string) Request.Form["Body"]!).Trim();

        // Create a response (optional)
        var response = new MessagingResponse();

        Person? person;
        switch (body.ToLower()) {
            case "y":
            case "yes":
                response.Message(
                    "Thank you for accepting the invitation! We are excited to see you at the courts!"
                );
                person = db.People.ToList().First(a => a.PhoneNumber == from);
                person.Availability = 1;
                break;
            case "n":
            case "no":
                response.Message(
                    "Thank you for your response! Hopefully we can see you down next time!"
                );
                person = db.People.ToList().First(a => a.PhoneNumber == from);
                person.Availability = 0;
                break;
            default:
                response.Message("I don't understand that message! Please respond with YES or NO.");
                break;
        }

        db.SaveChanges();
        return TwiML(response);
    }
}