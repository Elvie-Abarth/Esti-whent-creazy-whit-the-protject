using MarsvinWebExample.Models;
using Microsoft.Data.SqlClient;

namespace MarsvinWebExample.Data;

/// <summary>
/// What visitors send the shop through its two public forms - contact
/// messages and donations. Written by anyone (both forms are open to
/// guests), read only by staff.
/// </summary>
public interface IInboxStore
{
    void AddContactMessage(ContactMessage message);

    /// <summary>Most recent first - the Admin/Messages view.</summary>
    IReadOnlyList<ContactMessage> GetContactMessages(int count);

    void AddDonation(Donation donation);

    /// <summary>Most recent first - the Admin/Donations view.</summary>
    IReadOnlyList<Donation> GetDonations(int count);
}

public sealed class SqlInboxStore(string connectionString) : IInboxStore
{
    public void AddContactMessage(ContactMessage message)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            INSERT INTO dbo.ContactMessages (Name, Email, Topic, OrderId, Message)
            VALUES (@Name, @Email, @Topic, @OrderId, @Message);
            """, connection);
        command.Parameters.AddWithValue("@Name", message.Name);
        command.Parameters.AddWithValue("@Email", message.Email);
        command.Parameters.AddWithValue("@Topic", (byte)message.Topic);
        command.Parameters.AddWithValue("@OrderId", (object?)message.OrderId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Message", message.Message);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<ContactMessage> GetContactMessages(int count)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT TOP (@Count) ContactMessageId, Name, Email, Topic, OrderId, Message, CreatedAt " +
            "FROM dbo.ContactMessages ORDER BY CreatedAt DESC, ContactMessageId DESC;", connection);
        command.Parameters.AddWithValue("@Count", count);

        var messages = new List<ContactMessage>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new ContactMessage
            {
                ContactMessageId = reader.GetInt32(reader.GetOrdinal("ContactMessageId")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                Topic = (ContactTopic)reader.GetByte(reader.GetOrdinal("Topic")),
                OrderId = reader.GetNullableInt32("OrderId"),
                Message = reader.GetString(reader.GetOrdinal("Message")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            });
        }
        return messages;
    }

    public void AddDonation(Donation donation)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            """
            INSERT INTO dbo.Donations (Kind, AmountKr, ItemDescription, DonorName, DonorEmail, Message)
            VALUES (@Kind, @AmountKr, @ItemDescription, @DonorName, @DonorEmail, @Message);
            """, connection);
        command.Parameters.AddWithValue("@Kind", (byte)donation.Kind);
        command.Parameters.AddWithValue("@AmountKr", (object?)donation.AmountKr ?? DBNull.Value);
        command.Parameters.AddWithValue("@ItemDescription", (object?)donation.ItemDescription ?? DBNull.Value);
        command.Parameters.AddWithValue("@DonorName", (object?)donation.DonorName ?? DBNull.Value);
        command.Parameters.AddWithValue("@DonorEmail", (object?)donation.DonorEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("@Message", (object?)donation.Message ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<Donation> GetDonations(int count)
    {
        using var connection = Open();
        using var command = new SqlCommand(
            "SELECT TOP (@Count) DonationId, Kind, AmountKr, ItemDescription, DonorName, DonorEmail, Message, CreatedAt " +
            "FROM dbo.Donations ORDER BY CreatedAt DESC, DonationId DESC;", connection);
        command.Parameters.AddWithValue("@Count", count);

        var donations = new List<Donation>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var amountOrdinal = reader.GetOrdinal("AmountKr");
            donations.Add(new Donation
            {
                DonationId = reader.GetInt32(reader.GetOrdinal("DonationId")),
                Kind = (DonationKind)reader.GetByte(reader.GetOrdinal("Kind")),
                AmountKr = reader.IsDBNull(amountOrdinal) ? null : reader.GetDecimal(amountOrdinal),
                ItemDescription = reader.GetNullableString("ItemDescription"),
                DonorName = reader.GetNullableString("DonorName"),
                DonorEmail = reader.GetNullableString("DonorEmail"),
                Message = reader.GetNullableString("Message"),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            });
        }
        return donations;
    }

    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}
