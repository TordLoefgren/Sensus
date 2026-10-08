#include <Servo.h>

enum SampleStatus : int {
  Valid = 0,
  NoEcho = 1,
};

const int TRIG_PIN = 9;
const int ECHO_PIN = 10;
const int SERVO_PIN = 11;

const int PROTOCOL_REVISION = 1;

const int MIN_BEARING_DEGREES = -80;
const int MAX_BEARING_DEGREES = 80;
const int BEARING_STEP_DEGREES = 1;

const int MIN_SERVO_PULSE_WIDTH_US = 600;
const int MAX_SERVO_PULSE_WIDTH_US = 2400;

const unsigned int ACQUISITION_DELAY_MS = 100;
const unsigned long ECHO_TIMEOUT_US = 30000UL;
const unsigned int START_SETTLING_DELAY_MS = 500;

Servo servo;

bool hasHandshake = false;
bool isRunning = false;

unsigned long sequence = 1;
unsigned long sweepId = 1;
unsigned long elapsedUs = 0;
int targetBearingDegrees = MIN_BEARING_DEGREES;
unsigned long roundTripDurationUs = 0;
SampleStatus status = SampleStatus::Valid;

bool increasingBearing = true;

void resetScannerState() {
  sequence = 1;
  sweepId = 1;
  elapsedUs = 0;
  targetBearingDegrees = MIN_BEARING_DEGREES;
  roundTripDurationUs = 0;
  status = SampleStatus::Valid;

  increasingBearing = true;
  hasHandshake = false;
  isRunning = false;
}

int bearingToServoPulseWidthUs(int targetBearingDegrees) {
  // On this assembly, moving from left to right requires a shorter pulse.
  return static_cast<int>(
    map(
      targetBearingDegrees,
      MIN_BEARING_DEGREES,
      MAX_BEARING_DEGREES,
      MAX_SERVO_PULSE_WIDTH_US,
      MIN_SERVO_PULSE_WIDTH_US
    )
  );
}

void advanceSweep() {
  if (increasingBearing) {
    if (targetBearingDegrees >= MAX_BEARING_DEGREES) {
      increasingBearing = false;

      sweepId++;
      targetBearingDegrees -= BEARING_STEP_DEGREES;

      return;
    }

    targetBearingDegrees += BEARING_STEP_DEGREES;

    return;
  }

  if (targetBearingDegrees <= MIN_BEARING_DEGREES) {
    increasingBearing = true;

    sweepId++;
    targetBearingDegrees += BEARING_STEP_DEGREES;

    return;
  }

  targetBearingDegrees -= BEARING_STEP_DEGREES;
}

void moveServoToTargetBearing() {
  // Translate the scanner-level bearing into the actuator command required by this physical servo.
  auto servoPulseWidthUs = bearingToServoPulseWidthUs(targetBearingDegrees);
  servo.writeMicroseconds(servoPulseWidthUs);
}

void writeRangeSampleCsv(
  Print& output,
  unsigned long sequence,
  unsigned long sweepId,
  unsigned long elapsedUs,
  int bearingDegrees,
  unsigned long roundTripDurationUs,
  SampleStatus status
) {
  output.print(sequence);
  output.print(',');
  output.print(sweepId);
  output.print(',');
  output.print(elapsedUs);
  output.print(',');
  output.print(bearingDegrees);
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

  servo.attach(
    SERVO_PIN,
    MIN_SERVO_PULSE_WIDTH_US,
    MAX_SERVO_PULSE_WIDTH_US
  );

  moveServoToTargetBearing();

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

    moveServoToTargetBearing();

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

  moveServoToTargetBearing();

  // Give the servo time to move toward the target bearing.
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

  // The protocol reports scanner semantics rather than the actuator-specific pulse width.
  writeRangeSampleCsv(
    Serial,
    sequence,
    sweepId,
    elapsedUs,
    targetBearingDegrees,
    roundTripDurationUs,
    status
  );

  sequence++;
  advanceSweep();

  delay(ACQUISITION_DELAY_MS);
}
