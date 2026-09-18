const int TRIG_PIN = 9;
const int ECHO_PIN = 10;

unsigned long roundtripDurationUs;

void setup() {
    Serial.begin(9600);

    pinMode(TRIG_PIN, OUTPUT);
    pinMode(ECHO_PIN, INPUT);
}

void loop() {
  // Start with a clean signal.
  digitalWrite(TRIG_PIN, LOW);
  delayMicroseconds(2);

  // Send trigger signal.
  digitalWrite(TRIG_PIN, HIGH);
  delayMicroseconds(10);
  digitalWrite(TRIG_PIN, LOW);

  // Return roundtrip pulse duration in microseconds.
  roundtripDurationUs = pulseIn(ECHO_PIN, HIGH, 30000);

  Serial.println(roundtripDurationUs);

  delay(100);
}
