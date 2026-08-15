using Nurtricenter.MS3.Core.Common;

namespace Nurtricenter.MS3.Core.ValueObjects;

/// <summary>
/// Represents the physical label attached to a delivery package.
/// Contains tracking, patient, and delivery address information
/// sourced from MS1 (patient name) and MS4 (delivery address).
/// </summary>
public sealed class Label : ValueObject
{
    public string TrackingNumber { get; private set; }
    public string PatientName { get; private set; }
    public string DeliveryAddress { get; private set; }

    private Label() { } 

    public Label(string trackingNumber, string patientName, string deliveryAddress)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new ArgumentException("TrackingNumber cannot be empty.", nameof(trackingNumber));
        if (string.IsNullOrWhiteSpace(patientName))
            throw new ArgumentException("PatientName cannot be empty.", nameof(patientName));
        if (string.IsNullOrWhiteSpace(deliveryAddress))
            throw new ArgumentException("DeliveryAddress cannot be empty.", nameof(deliveryAddress));

        TrackingNumber = trackingNumber;
        PatientName = patientName;
        DeliveryAddress = deliveryAddress;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TrackingNumber.ToLowerInvariant();
        yield return PatientName.ToLowerInvariant();
        yield return DeliveryAddress.ToLowerInvariant();
    }
}
