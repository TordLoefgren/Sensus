#include <Servo.h>

enum SampleStatus : int {
  Valid = 0,
  NoEcho = 1,
};

const int TRIG_PIN = 9;
const int ECHO_PIN = 10;
const int SERVO_PIN = 11;

Servo servo;

bool hasHandshake = false;
bool isRunning = false;

unsigned long sequence = 1;
unsigned long sweepId = 1;
unsigned long elapsedUs = 0;
double bearingDegrees = -90;
unsigned long roundTripDurationUs = 0;
SampleStatus status = SampleStatus::Valid;

bool sweepingClockwise = true;

void resetScannerState() {
  sequence = 1;
  sweepId = 1;
  elapsedUs = 0;
  bearingDegrees = -90;
  roundTripDurationUs = 0;
  status = SampleStatus::Valid;

  sweepingClockwise = true;
  hasHandshake = false;
  isRunning = false;
}

bool updateSweepDirection() {
  auto directionChanged = false;

  if (sweepingClockwise && bearingDegrees >= 90) {
    sweepingClockwise = false;
    directionChanged = true;
  }
  if (!sweepingClockwise && bearingDegrees <= -90) {
    sweepingClockwise = true;
    directionChanged = true;
  }

  return directionChanged;
}

void writeRangeSampleCsv(
  Print& output,
  unsigned long sequence,
  unsigned long sweepId,
  unsigned long elapsedUs,
  double bearingDegrees,
  unsigned long roundTripDurationUs,
  SampleStatus status
) {
  output.print(sequence);
  output.print(',');
  output.print(sweepId);
  output.print(',');
  output.print(elapsedUs);
  output.print(',');
  output.print(bearingDegrees, 6);
  output.print(',');
  output.print(roundTripDurationUs);
  output.print(',');
  output.println(static_cast<int>(status));
}

void setup() {
  Serial.begin(9600);

  pinMode(TRIG_PIN, OUTPUT);
  pinMode(ECHO_PIN, INPUT);

  servo.attach(SERVO_PIN);

  servo.write(static_cast<int>(bearingDegrees + 90));
  delay(500);
}

void processIncomingCommand() {
  if (Serial.available() == 0) {
    return;
  }

  auto command = Serial.readStringUntil('\n');
  command.trim();

  if (command == "HELLO") {
    resetScannerState();

    hasHandshake = true;

    Serial.println("HELLO BACK");
    return;
  }

  if (hasHandshake && command == "START") {
    isRunning = true;
  }
}

void loop() {
  processIncomingCommand();

  if (!isRunning) {
    return;
  }

  // Move to the bearing that the current sample will represent.
  servo.write(static_cast<int>(bearingDegrees + 90));

  // Give the servo time to reach the new position.
  delay(20);

  // Start with a clean signal.
  digitalWrite(TRIG_PIN, LOW);
  delayMicroseconds(2);

  // Send trigger signal.
  digitalWrite(TRIG_PIN, HIGH);
  delayMicroseconds(10);
  digitalWrite(TRIG_PIN, LOW);

  // Measure the echo pulse in microseconds, waiting up to 30 ms.
  roundTripDurationUs = pulseIn(ECHO_PIN, HIGH, 30000);

  // A zero duration means no complete echo was received before the timeout.
  status = roundTripDurationUs == 0 ? SampleStatus::NoEcho : SampleStatus::Valid;
  elapsedUs = micros();

  writeRangeSampleCsv(
    Serial,
    sequence,
    sweepId,
    elapsedUs,
    bearingDegrees,
    roundTripDurationUs,
    status
  );

  // Advance the target bearing for the next measurement.
  sequence++;
  bearingDegrees += sweepingClockwise ? 1.0 : -1.0;

  if (updateSweepDirection()) {
    sweepId++;
  }

  // Acquisition delay between samples, in milliseconds.
  delay(100);
}
