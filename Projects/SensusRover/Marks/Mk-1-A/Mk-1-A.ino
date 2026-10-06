#include <Servo.h>

enum SampleStatus : int {
  Valid = 0,
  NoEcho = 1,
};

const int TRIG_PIN = 9;
const int ECHO_PIN = 10;
const int SERVO_PIN = 11;

const int PROTOCOL_REVISION = 1;

const int MIN_BEARING_DEGREES = -90;
const int MAX_BEARING_DEGREES = 90;
const int BEARING_STEP_DEGREES = 1;

const unsigned int ACQUISITION_DELAY_MS = 100;
const unsigned long ECHO_TIMEOUT_US = 30000UL;
const unsigned int START_SETTLING_DELAY_MS = 500;

Servo servo;

bool hasHandshake = false;
bool isRunning = false;

unsigned long sequence = 1;
unsigned long sweepId = 1;
unsigned long elapsedUs = 0;
double bearingDegrees = MIN_BEARING_DEGREES;
unsigned long roundTripDurationUs = 0;
SampleStatus status = SampleStatus::Valid;

bool sweepingClockwise = true;

void resetScannerState() {
  sequence = 1;
  sweepId = 1;
  elapsedUs = 0;
  bearingDegrees = MIN_BEARING_DEGREES;
  roundTripDurationUs = 0;
  status = SampleStatus::Valid;

  sweepingClockwise = true;
  hasHandshake = false;
  isRunning = false;
}

bool updateSweepDirection() {
  auto directionChanged = false;

  if (sweepingClockwise && bearingDegrees >= MAX_BEARING_DEGREES) {
    sweepingClockwise = false;
    directionChanged = true;
  }
  if (!sweepingClockwise && bearingDegrees <= MIN_BEARING_DEGREES) {
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

void writeProtocolMessage(Print& output, const __FlashStringHelper* message) {
  output.print(F("SENSUS,"));
  output.print(PROTOCOL_REVISION);
  output.print(',');
  output.println(message);
}

void writeHandshakeResponse(Print& output) {
  writeProtocolMessage(output, F("DESCRIPTION"));
  output.println(F("SCANNER,Sensus Rover,Mk. 1-A"));
  output.println(F("BOARD,ELEGOO UNO R3,ATmega328"));
  output.println(F("RANGE_SENSOR,HC-SR04,2,400,15"));
  output.println(F("SERVO,SG90,180"));
  output.print(F("CONFIGURATION,"));
  output.print(MIN_BEARING_DEGREES);
  output.print(',');
  output.print(MAX_BEARING_DEGREES);
  output.print(',');
  output.print(BEARING_STEP_DEGREES);
  output.print(',');
  output.print(ACQUISITION_DELAY_MS);
  output.print(',');
  output.println(ECHO_TIMEOUT_US);
  writeProtocolMessage(output, F("READY"));
}

void setup() {
  Serial.begin(9600);

  pinMode(TRIG_PIN, OUTPUT);
  pinMode(ECHO_PIN, INPUT);

  servo.attach(SERVO_PIN);

  servo.write(static_cast<int>(bearingDegrees - MIN_BEARING_DEGREES));
  delay(START_SETTLING_DELAY_MS);
}

void processIncomingCommand() {
  if (Serial.available() == 0) {
    return;
  }

  auto command = Serial.readStringUntil('\n');
  command.trim();

  String prefix = F("SENSUS,");
  prefix += PROTOCOL_REVISION;
  prefix += ',';

  if (!command.startsWith(prefix)) {
    return;
  }

  auto action = command.substring(prefix.length());

  if (action == "PREPARE") {
    resetScannerState();

    servo.write(static_cast<int>(bearingDegrees - MIN_BEARING_DEGREES));
    delay(START_SETTLING_DELAY_MS);
    writeHandshakeResponse(Serial);
    hasHandshake = true;
    return;
  }

  if (hasHandshake && action == "START") {
    isRunning = true;
    return;
  }

  if (action == "STOP") {
    resetScannerState();
    writeProtocolMessage(Serial, F("STOPPED"));
  }
}

void loop() {
  processIncomingCommand();

  if (!isRunning) {
    return;
  }

  // Move to the bearing that the current sample will represent.
  servo.write(static_cast<int>(bearingDegrees - MIN_BEARING_DEGREES));

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
  roundTripDurationUs = pulseIn(ECHO_PIN, HIGH, ECHO_TIMEOUT_US);

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
  bearingDegrees += sweepingClockwise ? BEARING_STEP_DEGREES : -BEARING_STEP_DEGREES;

  if (updateSweepDirection()) {
    sweepId++;
  }

  // Acquisition delay between samples, in milliseconds.
  delay(ACQUISITION_DELAY_MS);
}
