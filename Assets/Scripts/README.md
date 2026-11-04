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

## Análise (Parte D)

> Preencher com os números da sua execução. Seed, geração e fitness de cada rodada aparecem no Console e no painel.

**1. O melhor fitness correspondeu à rota mais eficiente?**
Compare o melhor da geração (Console) com outro indivíduo/geração de fitness menor e eficiência maior.

- Caso: _geração __, fitness __, eficiência __ versus geração __, fitness __, eficiência __._
- Conclusão: _sim/não, com os números acima._

**2. Em qual cenário de validação o agente foi pior? Por quê?**

- Cenário: _nome, resultado (chegou/colidiu), eficiência._
- Motivo: _por exemplo, o obstáculo novo ficou fora do campo dos 3 sensores (35° de abertura, 8 m), ou o Goal ficou do outro lado da curva que o treino nunca exigiu._

**3. Qual alteração no fitness poderia induzir um comportamento indesejado?**

O progresso é a **distância em linha reta** até o Goal, medida a cada passo. Se a penalidade de colisão for reduzida ou zerada (ou o peso do progresso aumentado), o atalho passa a ser bater de propósito na parede mais próxima do Goal: o episódio termina com bom progresso e quase nenhuma punição, sem aprender o contorno. O mesmo vale para recompensar tempo de sobrevivência: a nave pode girar em círculos até o timeout. Sem recompensa por rapidez, o fitness também não distingue uma rota direta de uma rota longa que chega ao alvo.

**4. O genoma salvo reproduziu o comportamento depois de fechar e reabrir a cena?**

- Seed: _  | Geração: _  | Fitness salvo: _  | Fitness ao reexecutar: _
- Resultado da reexecução (chegou/colidiu/tempo/eficiência): _
- Observação: a física da Unity não é determinística entre execuções, então pequenas diferenças de fitness e trajetória são esperadas.
