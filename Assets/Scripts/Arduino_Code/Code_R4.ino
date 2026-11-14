#include <LiquidCrystal_I2C.h>
#include <math.h>

enum GameState {
  ACTIVE,
  ENDED
};

enum WinState {
  WON,
  LOST,
  NOT_CONCLUDED
};

#define GAME_TIME 301
#define WIN_DIGIT 42
#define APin 2
#define BPin 3
#define CPin 4
#define DPin 5
#define EPin 6
#define FPin 7
#define GPin 8
#define HPin 9
#define CheckPin 13

#define LED_PROX_1 10
#define LED_PROX_2 11
#define LED_PROX_3 12
#define LED_WIN A0
#define LED_LOSE A1
#define BUZZER_LOSE A2

#define WRONG_COUNTER 10
#define MAX_TIME_COUNTDOWN 1000
#define MIN_TIME_COUNTDOWN 1

LiquidCrystal_I2C lcd(0x27, 16, 2);

int timeLeft;
int A{}, B{}, C{}, D{}, E{}, valF{}, G{}, H{}, Check{};
int LastReadCheck{};
GameState currentState;
WinState currentWinState;
unsigned long readInputMs, constructBinaryMs, checkWinConditionMs, updateTimerMs, updateLCDDisplayMs, showWrongMessageMs, pushInfoMs, updateIndicatorsMs;
char binaryConstruct[9];
bool ShowWrong{};
int WrongCounter = WRONG_COUNTER;
int timerCountDown{};

int lastKnownTime{};
char lastKnownBinary[9] = "";
int currentDecimalValue{};
int Prox_1{}, Prox_2{}, Prox_3{};

int lastGameState = -1;
int lastWinState = -1;
bool lastShowWrong = false;
int lastTimeSent = -1;
char lastSentBinary[9] = "";
int lastProx1 = -1;
int lastProx2 = -1;
int lastProx3 = -1;

bool waitMillis(unsigned long* t, long waitMillis) {
  if ((millis() - *t) > waitMillis) {
    *t = millis();
    return true;
  }
  if (millis() < *t) *t = millis();
  return false;
}

float getBellCurveValue(float x) {
  float w = 400.0;
  float diff = x - 42.0;
  return exp(-(diff * diff) / w);
}

void readInput() {
  A = digitalRead(APin);
  B = digitalRead(BPin);
  C = digitalRead(CPin);
  D = digitalRead(DPin);
  E = digitalRead(EPin);
  valF = digitalRead(FPin);
  G = digitalRead(GPin);
  H = digitalRead(HPin);
  int CheckRead = digitalRead(CheckPin);

  if (CheckRead == HIGH && LastReadCheck == LOW) {
    if (Check == HIGH) Check = LOW;
    else if (Check == LOW) Check = HIGH;
  }
  LastReadCheck = CheckRead;
}

void constructBinary() {
  currentDecimalValue = (A << 7) | (B << 6) | (C << 5) | (D << 4) | (E << 3) | (valF << 2) | (G << 1) | H;
  snprintf(binaryConstruct, sizeof(binaryConstruct), "%d%d%d%d%d%d%d%d", A, B, C, D, E, valF, G, H);
}

void triggerLoseState() {
  currentState = ENDED;
  currentWinState = LOST;
  digitalWrite(LED_LOSE, HIGH);
  tone(BUZZER_LOSE, 500);
}

void triggerWinState() {
  currentState = ENDED;
  currentWinState = WON;
  digitalWrite(LED_WIN, HIGH);
}

void updateTimer() {
  timeLeft -= 1;
  if (timeLeft <= 0) {
    timeLeft = 0;
    triggerLoseState();
  }
}

void updateLCDDisplay() {
  if (strcmp(binaryConstruct, lastKnownBinary) != 0) {
    strcpy(lastKnownBinary, binaryConstruct);
    lcd.setCursor(7, 0);
    lcd.print("        ");
    lcd.setCursor(7, 0);
    lcd.print(binaryConstruct);
  }

  if (timeLeft != lastKnownTime) {
    lastKnownTime = timeLeft;
    lcd.setCursor(11, 1);
    lcd.print("    ");
    lcd.setCursor(11, 1);
    lcd.print(timeLeft);
    lcd.print("s");
  }

  lcd.setCursor(0, 1);
  if (ShowWrong == false) {
    if (currentState != ENDED) lcd.print("READY  ");
    else if (currentWinState == WON) lcd.print("WIN!   ");
    else if (currentWinState == LOST) lcd.print("LOST!  ");
  }
}

void showWrongMessage() {
  if (ShowWrong == true && WrongCounter > 0) {
    lcd.setCursor(0, 1);
    lcd.print("WRONG  ");
    WrongCounter--;
  } else if (WrongCounter <= 0) {
    ShowWrong = false;
    WrongCounter = WRONG_COUNTER;
  }
}

void checkWinCondition() {
  if (Check == LOW) return;

  if (currentDecimalValue == WIN_DIGIT) {
    triggerWinState();
    return;
  }

  Check = LOW;
  timerCountDown = timerCountDown / 2;
  if (timeLeft <= 0) {
    timeLeft = 0;
    triggerLoseState();
  }
  ShowWrong = true;

  updateLCDDisplay();
  showWrongMessage();
}

void updateProximityIndicators() {
  if (currentState == ENDED) {
    digitalWrite(LED_PROX_1, LOW);
    digitalWrite(LED_PROX_2, LOW);
    digitalWrite(LED_PROX_3, LOW);
    return;
  }

  float proximity = getBellCurveValue((float)currentDecimalValue);

  Prox_1 = proximity > 0.1 ? HIGH : LOW;
  Prox_2 = proximity > 0.5 ? HIGH : LOW;
  Prox_3 = proximity > 0.8 ? HIGH : LOW;
  digitalWrite(LED_PROX_1, Prox_1);
  digitalWrite(LED_PROX_2, Prox_2);
  digitalWrite(LED_PROX_3, Prox_3);
}

void pushGameState() {
  char jsonBuffer[80];
  snprintf(jsonBuffer, sizeof(jsonBuffer), "{\"type\":1,\"GAME_STATE\":%d,\"WIN_STATE\":%d,\"WRONG\":%d}", currentState, currentWinState, ShowWrong);
  Serial.println(jsonBuffer);
}

void pushTime() {
  char jsonBuffer[40];
  snprintf(jsonBuffer, sizeof(jsonBuffer), "{\"type\":2,\"TIME\":%d}", timeLeft);
  Serial.println(jsonBuffer);
}

void pushSwitches() {
  char jsonBuffer[120];
  snprintf(jsonBuffer, sizeof(jsonBuffer), "{\"type\":3,\"A\":%d,\"B\":%d,\"C\":%d,\"D\":%d,\"E\":%d,\"F\":%d,\"G\":%d,\"H\":%d}", A, B, C, D, E, valF, G, H);
  Serial.println(jsonBuffer);
}

void pushProximity() {
  char jsonBuffer[80];
  snprintf(jsonBuffer, sizeof(jsonBuffer), "{\"type\":4,\"PROX_1\":%d,\"PROX_2\":%d,\"PROX_3\":%d}", Prox_1, Prox_2, Prox_3);
  Serial.println(jsonBuffer);
}

void checkAndPushUART() {
  if (currentState != lastGameState || currentWinState != lastWinState || ShowWrong != lastShowWrong) {
    pushGameState();
    lastGameState = currentState;
    lastWinState = currentWinState;
    lastShowWrong = ShowWrong;
  }
  if (timeLeft != lastTimeSent) {
    pushTime();
    lastTimeSent = timeLeft;
  }
  if (strcmp(binaryConstruct, lastSentBinary) != 0) {
    pushSwitches();
    strcpy(lastSentBinary, binaryConstruct);
  }
  if (Prox_1 != lastProx1 || Prox_2 != lastProx2 || Prox_3 != lastProx3) {
    pushProximity();
    lastProx1 = Prox_1;
    lastProx2 = Prox_2;
    lastProx3 = Prox_3;
  }
}

void setup() {
  Serial.begin(115200);

  timeLeft = GAME_TIME;
  currentState = ACTIVE;
  currentWinState = NOT_CONCLUDED;
  readInputMs = millis();
  constructBinaryMs = millis();
  checkWinConditionMs = millis();
  updateTimerMs = millis();
  updateLCDDisplayMs = millis();
  showWrongMessageMs = millis();
  pushInfoMs = millis();
  updateIndicatorsMs = millis();
  timerCountDown = MAX_TIME_COUNTDOWN;

  pinMode(APin, INPUT);
  pinMode(BPin, INPUT);
  pinMode(CPin, INPUT);
  pinMode(DPin, INPUT);
  pinMode(EPin, INPUT);
  pinMode(FPin, INPUT);
  pinMode(GPin, INPUT);
  pinMode(HPin, INPUT);
  pinMode(CheckPin, INPUT);

  pinMode(LED_PROX_1, OUTPUT);
  pinMode(LED_PROX_2, OUTPUT);
  pinMode(LED_PROX_3, OUTPUT);
  pinMode(LED_WIN, OUTPUT);
  pinMode(LED_LOSE, OUTPUT);
  pinMode(BUZZER_LOSE, OUTPUT);

  lcd.init();
  lcd.backlight();

  lcd.setCursor(0, 0); lcd.print("Value: ");
  lcd.setCursor(0, 1); lcd.print("READY");
}

void loop() {
  if (waitMillis(&readInputMs, 100) && currentState != ENDED) readInput();
  if (waitMillis(&constructBinaryMs, 100) && currentState != ENDED) constructBinary();
  if (waitMillis(&checkWinConditionMs, 100) && currentState != ENDED) checkWinCondition();
  if (waitMillis(&updateIndicatorsMs, 100)) updateProximityIndicators();
  if (waitMillis(&updateTimerMs, timerCountDown) && currentState != ENDED) updateTimer();
  if (waitMillis(&updateLCDDisplayMs, 200)) updateLCDDisplay();
  if (waitMillis(&showWrongMessageMs, 200)) showWrongMessage();
  if (waitMillis(&pushInfoMs, 50)) checkAndPushUART();
}