# Neuroevolução - Unity 6

Scripts para o laboratório da Aula 05. O controlador é uma rede neural `3-4-2` cujos 26 pesos e biases são evoluídos por um algoritmo genético.

## Arquivos

- `Genome.cs`: vetor de pesos, crossover e mutação.
- `NeuralNetwork.cs`: inferência da rede neural, sem alocações durante o episódio.
- `EvolvingShip.cs`: Raycasts, movimento, fitness individual e término de episódio.
- `EvolutionManager.cs`: população, elitismo, torneio e troca de gerações; também executa o genoma salvo e a validação.
- `ReferenceRoute.cs`: rota de referência (grid navegável + A*), usada só para comparação.
- `EpisodeMetrics.cs`: métricas de um episódio (chegada, colisão, tempo, distâncias, eficiência).
- `ValidationScenario.cs`: cenário de validação configurado no Inspector e o resultado de cada cenário.
- `EvolutionSnapshot.cs` / `BestGenomeStore.cs`: formato do save e persistência com `PlayerPrefs`.
- `EvolutionPanel.cs`: painel com os botões e a tabela de validação (é adicionado sozinho ao `EvolutionManager`).

## Configuração da cena

1. Crie uma cena 3D com um plano, paredes e obstáculos com `Collider`. Crie a layer `Obstacle` e aplique-a a todas as paredes e obstáculos.
2. Crie um objeto `Goal` com `Collider` marcado como `Is Trigger`. O `Transform` desse objeto será o campo **Goal** do manager.
3. Crie um objeto vazio `SpawnPoint` posicionado no início da pista.
4. Crie o prefab `Ship`: um objeto com `Rigidbody`, `Collider` e `EvolvingShip`. Desative `Use Gravity` no Rigidbody. Em **Obstacle Mask**, selecione somente `Obstacle`.
5. Crie um objeto vazio `EvolutionManager`, anexe `EvolutionManager.cs` e associe o prefab, `SpawnPoint` e `Goal`.
6. No `Project Settings > Physics`, desative colisão entre a layer usada pelas naves e ela mesma. Assim, os indivíduos não atrapalham a avaliação uns dos outros.
7. Execute a cena. O Console registra o melhor e o fitness médio a cada geração.

## Fitness padrão

`progresso ate o alvo * 10 + bonus ao chegar - 50 por colisao - 1 por timeout`

O progresso é recalculado continuamente. Assim, uma nave parada não recebe fitness alto apenas por sobreviver.

## Experimentos sugeridos

- Comece com `populationSize = 30`, `mutationRate = 0.08` e `mutationStrength = 0.25`.
- Varie uma configuração por vez e anote melhor fitness, fitness médio e geração em que o comportamento surgiu.
- Mude o `randomSeed` para repetir ou comparar execuções.
- Para validar generalização, mova o alvo depois de encontrar um bom genoma e rode novamente.

## Rota de referência (Parte A)

Escolha: **grid + A\***, sem NavMesh, para funcionar em qualquer cenário sem bake.

- O grid cobre os colliders da layer `Obstacle` (mais Spawn e Goal). Uma célula é navegável se uma caixa de lado `Reference Agent Radius * 2` centrada nela não toca nenhum obstáculo.
- A* com 8 vizinhos, sem cortar quina. Com `Smooth Reference` ligado, pontos intermediários com linha de visada livre são removidos e sobram os cantos do caminho.
- O comprimento da rota é a soma dos segmentos `Spawn -> cantos -> Goal` no plano XZ.
- A rota é recalculada a cada troca de cenário e desenhada em amarelo como Gizmo. Ela **não controla** a nave.
- Se Spawn ou Goal estiverem bloqueados, usa-se a célula livre mais próxima (até 8 células). Se não houver caminho, aparece um aviso e a eficiência fica `n/d`.

Parâmetros no Inspector: `Reference Cell Size`, `Reference Agent Radius` (use o raio do collider da nave) e `Smooth Reference`.

## Métricas por episódio

| Métrica | Como é medida |
|---|---|
| Chegou / Colidiu | primeiro evento do episódio (goal, obstáculo ou timeout) |
| Tempo | soma de `Time.fixedDeltaTime` dos passos em que a nave esteve ativa |
| Distância real | soma dos deslocamentos entre passos de física consecutivos |
| Distância de referência | comprimento da rota A* do cenário |
| Eficiência | `distância de referência / distância real`, só se chegou ao alvo e as duas distâncias são > 0; senão `n/d` |

A eficiência pode passar de 1,00 por pouco: o episódio termina ao tocar o trigger do Goal, e a referência vai até o centro dele. A cada geração, o Console mostra as métricas do melhor da geração.

**Fitness:** não foi alterado (progresso + bônus de chegada − penalidade de colisão − penalidade de timeout).

## Validação em cenários novos (Parte B)

Configure em `EvolutionManager > Validacao` uma lista com 3 cenários. Cada um tem:

- `Goal Marker` (opcional): um objeto vazio na posição do Goal naquele cenário. Vazio = mesma posição do treino.
- `Layout Root` (opcional): objeto pai com obstáculos extras, **na layer `Obstacle`**. Fica ativo só durante aquele cenário (o manager o desativa no treino).

Todos os cenários precisam ter caminho entre o Spawn e o Goal. Se a rota de referência não for encontrada, o Console avisa.

Fluxo: treine com a seed do Inspector, deixe algumas gerações passarem e clique em **Validar nos cenarios**. O melhor genoma já visto (cópia dos pesos) roda uma vez em cada cenário, sem evoluir. Ao final, a tabela aparece no painel e no Console. O projeto é considerado **VALIDADO** se o genoma chegar sem colidir em pelo menos 2 dos 3 cenários. Depois, o cenário de treino é restaurado e o botão **Retomar treino** continua o treino.

## Salvar e restaurar (Parte C)

Botões do painel:

- **Salvar melhor**: grava o melhor genoma já visto na chave `neuroevolution.best.v1` (JSON via `JsonUtility`, com `PlayerPrefs.Save()`). Campos: versão, seed, geração em que o genoma foi encontrado, melhor fitness, `weights` (cópia), população, taxa e força de mutação e data/hora.
- **Carregar melhor**: lê o save e executa o genoma em uma única execução, sem criar geração nova. Sem save, versão diferente, JSON inválido, quantidade de pesos diferente de `NeuralNetwork.WeightCount` (26) ou pesos NaN/infinito: mensagem clara (Console e painel), sem exceção, e o treino continua.
- **Apagar save**: chama `PlayerPrefs.DeleteKey` e confirma no Console e no painel.

Para testar o save depois de fechar e reabrir a cena: salve, pare o Play, entre de novo e clique em **Carregar melhor**.

## Resultados da execução (cena `MachineLearningScene`)

**Configuração usada**

| Parâmetro | Valor |
|---|---|
| Seed (`Random Seed`) | 1 |
| População / elitismo | 60 / 2 |
| Taxa / força de mutação | 0,15 / 0,4 |
| Episódio | 15 s |
| Fitness | inalterado (progresso x10, +100 chegada, -50 colisão, -1 timeout) |
| Rota de referência | grid + A*, célula 0,5, raio 0,5 |

**Ajustes feitos durante os testes (registrados por transparência)**

- A configuração inicial (população 30, mutação 0,08, força 0,25, seed 24749) travou em máximo local: em 254 gerações o melhor fitness ficou em torno de 134 e nenhuma nave chegou ao objetivo. As naves se agrupavam na parede mais próxima do Goal em linha reta. Por isso passei para população 60, mutação 0,15 e força 0,4. Com isso as seeds 1, 2 e 3 chegaram ao objetivo entre as gerações 80 e 130.
- Na primeira versão dos cenários, as seeds 1, 2 e 3 validaram só 1 de 3. Os cubos do Cenario2 e do Cenario3 fechavam um corredor inteiro (escala Z 8,1) e a rota de referência do Cenario2 subia para 47,2 (treino: 34,4). Encurtei os dois cubos para escala Z 3, mantendo o obstáculo na pista, e rodei a validação de novo.
- A cena `SampleScene` original não foi alterada. O trabalho foi feito numa cópia (`SampleScene_Validacao`, depois renomeada para `MachineLearningScene`).

**Cenários de validação (como configurados)**

| Cenário | Goal | Obstáculo |
|---|---|---|
| Cenario1 | `GoalMarker_1` (-13,3; 10,8), outro canto da pista | nenhum |
| Cenario2 | `GoalMarker_2` (-13,9; -10,2), perto do Goal do treino (-12,1; -11,0) | `Cube (10)` encurtado (Z 3) no corredor central |
| Cenario3 | `GoalMarker_3` (-11,3; -7,3) | `Cube (11)` encurtado (Z 3) perto do Goal |

**Treino com a seed 1 (execução gravada no vídeo):** o melhor genoma foi encontrado na geração 96 (fitness 417,24). No cenário de treino ele chega ao alvo em 2,4 s, com distância real 35,0 contra 34,4 de referência (eficiência 0,98). Execuções anteriores com a mesma seed deram a geração 177 e fitness 418,39: a física do Unity não é determinística, então a mesma seed não repete exatamente a mesma trajetória.

**Tabela de validação do mesmo genoma (sem evoluir):**

| Cenário | Chegou | Colidiu | Tempo (s) | Dist. real | Dist. ref | Eficiência |
|---|---|---|---|---|---|---|
| Cenario1 | sim | não | 5,1 | 56,6 | 28,8 | 0,51 |
| Cenario2 | não | sim | 1,5 | 15,1 | 35,5 | n/d |
| Cenario3 | sim | não | 2,3 | 32,7 | 31,7 | 0,97 |

**Veredito: VALIDADO, 2/3 cenários** (Cenario1 e Cenario3 chegaram sem colisão).

## Vídeo de demonstração

Arquivo `Video da validação.mp4` (cerca de 1 min 17 s). Mostra, na aba Game e no Console:

1. Treino com a seed 1 (Time Scale 50 só para acelerar o treino), com o melhor fitness e as métricas do melhor de cada geração.
2. Time Scale de volta a 1 e clique em **Validar nos cenarios**: o mesmo genoma roda nos 3 cenários, sem evoluir, com a rota de referência em amarelo.
3. A tabela de validação no painel e no Console, com seed, geração e fitness do genoma, e o veredito VALIDADO (2/3).
4. Save: **Salvar melhor**, **Apagar save**, **Carregar melhor** sem save (aviso, sem exceção), salvar de novo.
5. Novo Play (geração 0) e **Carregar melhor**: o genoma salvo roda sem criar geração nova e chega ao alvo.

## Análise (Parte D)

**1. O melhor fitness correspondeu à rota mais eficiente?**
Não. Contraexemplo, medido em execuções anteriores (não está no vídeo) com a mesma configuração (população 60, mutação 0,15, força 0,4):

- Seed 2, geração 131: fitness **418,28**, distância real 107,8 contra 34,4 de referência, eficiência ≈ **0,32**.
- Seed 1, geração 105: fitness **417,63**, distância real 34,9, eficiência ≈ **0,99**.

O fitness maior (418,28) pertence à rota três vezes mais longa. O fitness só mede quanto a nave se aproximou do Goal em linha reta mais o bônus de chegada, e não penaliza o caminho percorrido. Dois genomas que chegam têm fitness quase igual, mesmo com rotas muito diferentes.

**2. Em qual cenário de validação o agente foi pior? Por quê?**
No **Cenario2**: colidiu aos 1,5 s depois de 15,1 de distância, de 35,5 de rota de referência. O obstáculo novo fica no corredor central, por onde o genoma passava no treino, e a rede só enxerga 3 raios (abertura de 35°, alcance de 8 m) e não sabe onde está o Goal. Ela repete a sequência de curvas aprendida e não consegue desviar de um obstáculo que não existia no treino. Não medi qual sensor disparou, então esse é o motivo mais provável, e não um fato verificado. Na versão anterior dos cenários (cubo com Z 8,1), o Cenario2 também falhou nas seeds 1, 2 e 3, estourando o tempo com 87 a 167 de distância percorrida.
Nota: no Cenario1 o genoma chegou, mas com eficiência baixa (0,51; 56,6 contra 28,8), ou seja, deu uma volta enorme para uma rota curta.

**3. Qual alteração no fitness poderia induzir um comportamento indesejado?**
O progresso é a **distância em linha reta** até o Goal, medida a cada passo. Se a penalidade de colisão for reduzida ou zerada (ou o peso do progresso aumentado), o atalho passa a ser bater de propósito na parede mais próxima do Goal: o episódio termina com bom progresso e quase nenhuma punição, sem aprender o contorno. Esse problema apareceu de forma parecida na configuração inicial, em que as naves ficavam coladas na parede mais próxima do Goal em linha reta, com fitness estagnado em ~134. O mesmo vale para recompensar tempo de sobrevivência: a nave pode girar em círculos até o timeout. Sem recompensa por rapidez, o fitness também não distingue uma rota direta de uma rota longa que chega ao alvo (ver pergunta 1).

**4. O genoma salvo reproduziu o comportamento depois de fechar e reabrir a cena?**

- Seed: **1** | Geração: **96** | Fitness salvo: **417,24** | Fitness ao reexecutar: **417,18**
- Resultado da reexecução: chegou = sim, colidiu = não, tempo 2,3 s, distância real 35,0 contra 34,4 de referência, eficiência **0,98**. O Console registrou "Save carregado: seed 1, geração 96, fitness 417,24, salvo em 2026-11-24 10:13:52." e depois "Genoma salvo executado". O fitness de 417,18 contra 417,24 é a diferença esperada de uma física que não é determinística; o comportamento (rota, chegada, sem colisão) foi o mesmo.
- Como foi testado: salvei, parei o Play, entrei em Play de novo (a geração voltou a 0, como mostra o vídeo) e cliquei em **Carregar melhor**. Não fechei o Unity por completo; o `PlayerPrefs` grava em disco, mas o teste com o Editor fechado e reaberto não foi feito.
- Outros testes do save, no Console: **Apagar save** registrou "Save apagado ('neuroevolution.best.v1')", e **Carregar melhor** sem save mostrou o aviso "Nenhum save encontrado ('neuroevolution.best.v1'). Use 'Salvar melhor' antes de carregar.", sem exceção e sem interromper o fluxo.
