# Geração automática de testes unitários com LLM — material suplementar

Este repositório reúne o material suplementar do Trabalho de Conclusão de Curso
**"Geração automática de testes unitários para sistemas legados em C# com modelos
de linguagem de grande porte (LLM)"**, desenvolvido por **Lucca Maia Rosa** no curso
de Análise e Desenvolvimento de Sistemas da **UNISINOS**, sob orientação do
**Prof. Roberto Zanoni**.

O objetivo é dar **reprodutibilidade** ao experimento: aqui estão os testes unitários
gerados pelo LLM e o template de prompt utilizado.

## O que tem neste repositório

```
.
├── prompts/                 # Template de prompt zero-shot utilizado na geração
│   └── template-prompt-zero-shot.txt
└── TestsProject/            # Testes unitários gerados pelo LLM (NUnit + Moq)
    ├── M1_RoundPriceTests.cs
    ├── M2_GetTotalStockQuantityTests.cs
    ├── M3_PriceFormatterTests.cs
    ├── M4_ValidateDiscountTests.cs
    └── M5_GetFinalPriceTests.cs
```

> Observação: o código-fonte do sistema sob teste (nopCommerce 3.90) **não** é
> redistribuído aqui. Ele é open source e está disponível no repositório oficial
> do projeto. Este repositório contém apenas os artefatos produzidos no experimento.

## Contexto do experimento

O estudo de caso usou o **nopCommerce 3.90** (e-commerce open source em C#), que já
possui um conjunto maduro de testes escritos pela comunidade, utilizado como
*baseline* humano para comparação. Cinco métodos do módulo `Nop.Services`, de
complexidade ciclomática variada, foram selecionados. Para cada um, o modelo gerou
testes unitários em uma única tentativa, com a estratégia **zero-shot** (sem exemplos
de teste no prompt), e o código foi compilado e executado **sem qualquer correção
manual** prévia à medição.

## Métodos avaliados e resultados

| ID | Classe | Método | CC | Gerados | Compilaram | Passaram |
|----|--------|--------|----|---------|------------|----------|
| M1 | RoundingHelper | RoundPrice | 11 | 57 | 57 | 56 |
| M2 | ProductExtensions | GetTotalStockQuantity | 6 | 15 | 15 | 15 |
| M3 | PriceFormatter | FormatPrice | 5 | 28 | 28 | 0 |
| M4 | DiscountService | ValidateDiscount | 22 | 28 | 28 | 0 |
| M5 | PriceCalculationService | GetFinalPrice | 15 | 20 | 0 | 0 |
| **Total** | | | | **148** | **128 (86,5%)** | **71** |

CC = complexidade ciclomática (McCabe) medida no método de produção.

As falhas foram categorizadas em quatro tipos, todos relacionados a características
do código legado e não à sintaxe dos testes: erro semântico de regra de negócio (M1),
dependência estática oculta (M3), limitação técnica de mocking com Moq (M4) e violação
de encapsulamento (M5).

## Ambiente

- **Sistema sob teste:** nopCommerce, tag `release-3.90`, atualizado para .NET Framework 4.8
- **SDK:** .NET SDK 10.0.203
- **IDE:** Visual Studio Community
- **Frameworks de teste:** NUnit 3.x, NUnit3TestAdapter, Microsoft.NET.Test.Sdk
- **Mocking:** Moq 4.18.4
- **Modelo de linguagem:** Claude Sonnet 4.6 (estratégia zero-shot)

## Como reproduzir

1. Clone o nopCommerce na tag `release-3.90` e atualize o targeting para .NET Framework 4.8.
2. Adicione um projeto de testes `Nop.Services.Tests.Generated`, referenciando
   `Nop.Core`, `Nop.Services` e `Nop.Tests`, com os pacotes listados acima.
3. Copie os arquivos de `TestsProject/` para o projeto de testes.
4. Execute, por método, por exemplo:
   ```bash
   dotnet test --filter "FullyQualifiedName~RoundingHelperTests"
   ```

## Citação

Se este material for útil, cite o trabalho de origem (TCC, UNISINOS, 2026).

## Licença

Os artefatos deste repositório são disponibilizados para fins acadêmicos e de
reprodutibilidade. O nopCommerce possui sua própria licença, respeitada em seu
repositório oficial.
