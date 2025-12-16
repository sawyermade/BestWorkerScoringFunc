# BestWorkerScoringFunc

## Clone and Run
```bash
git clone https://github.com/sawyermade/BestWorkerScoringFunc.git 
cd BestWorkerScoringFunc
func start # Azurite must also be running
```

## Curl Command
```bash
curl -X POST "http://localhost:7071/api/ScoreWorker" \
  -H "Content-Type: application/json" \
  -d @test_00-base.json
```