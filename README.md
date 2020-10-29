# Tabu-Search-in-VRP
Algorythm of Tabu Search in Vehicle Routing Problem (VRP).__

Visual Studio Community 2019__
.Net Framework 4.8__
WPF Application__

# Algorithm description

**move** - moving from one solution to another one - in my algoryhm it is defined as swaping places of 2 points (clients) in Hamilton cycle. For example: 1,2,3,4,5,6,1  ->  1,5,3,4,2,6,1.__
**neighborhood** - all solutions which can be obtained by 1 move from the current solution.__
**Hamilton cycle** - it is cycle where each vertex of graph is visited exactly 1 time.__
**solution** - in my algorythm it is current Hamilton cycle.__
best solution - the best Hamilton cycle.__

1. Entering parameters.
2. Randomizing a set of clients, loading saved set or loading Solomon database.
3. Initialization.
4. Randomizing a first Hamilton cycle.
5. Tabu Search Algorithm:
	5.1. Checking neighborhood - algoryth is searching for better solution.
	- aspiration plus - if algorythm found better local solution that global solution, then it continue searching for a "plus" iterations .
	- if algorythm can't find new better solution it starts searching from new random Hamilton cycle.
	5.2. Adding new forbidden move to Tabu list.
6. Cutting best Hamilton cycle.
