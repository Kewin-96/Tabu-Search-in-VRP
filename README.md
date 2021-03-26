# Tabu Search in VRP
Algorythm of Tabu Search in Vehicle Routing Problem (VRP).  <br />

**Visual Studio Community 2019** <br />
**.Net Framework 4.8**  <br />
**WPF Application**  <br />

# Algorithm description

**move** - moving from one solution to another one - in my algoryhm it is defined as swaping places of 2 points (clients) in Hamilton cycle. For example: 1,**2**,3,4,**5**,6,1  ->  1,**5**,3,4,**2**,6,1.  <br />
**neighborhood** - all solutions which can be obtained by 1 move from the current solution.  <br />
**Hamilton cycle** - it is cycle where each vertex of graph is visited exactly 1 time.  <br />
**solution** - in my algorythm it is current Hamilton cycle.  <br />
best solution - the best Hamilton cycle.  <br />

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

# Algorithm presentation
https://youtu.be/kBAVmgAukBI

[![IMAGE ALT TEXT HERE](https://img.youtube.com/vi/kBAVmgAukBI/0.jpg)](https://www.youtube.com/watch?v=kBAVmgAukBI)
