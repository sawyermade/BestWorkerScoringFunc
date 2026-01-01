# BestWorkerScoringFunc

## Clone and Run
```bash
git clone https://github.com/sawyermade/BestWorkerScoringFunc.git 
cd BestWorkerScoringFunc
azurite # Run in separate window
func start # Runs in separate window
ngrok http 8080 # Runs in separate window
```

## Curl Command
```bash
curl -X POST "http://localhost:7071/api/ScoreWorker" \
  -H "Content-Type: application/json" \
  -d @test_00-base.json
```