import { Component } from '@angular/core';

interface ChessPiece {
  id: string;
  type: string;
  x: number;
  y: number;
}

const PIECE_URLS: Record<string, string> = {
  'wk': 'https://upload.wikimedia.org/wikipedia/commons/4/42/Chess_klt45.svg',
  'wq': 'https://upload.wikimedia.org/wikipedia/commons/1/15/Chess_qlt45.svg',
  'wr': 'https://upload.wikimedia.org/wikipedia/commons/7/72/Chess_rlt45.svg',
  'wb': 'https://upload.wikimedia.org/wikipedia/commons/b/b1/Chess_blt45.svg',
  'wn': 'https://upload.wikimedia.org/wikipedia/commons/7/70/Chess_nlt45.svg',
  'wp': 'https://upload.wikimedia.org/wikipedia/commons/4/45/Chess_plt45.svg',
  'bk': 'https://upload.wikimedia.org/wikipedia/commons/f/f0/Chess_kdt45.svg',
  'bq': 'https://upload.wikimedia.org/wikipedia/commons/4/47/Chess_qdt45.svg',
  'br': 'https://upload.wikimedia.org/wikipedia/commons/f/ff/Chess_rdt45.svg',
  'bb': 'https://upload.wikimedia.org/wikipedia/commons/9/98/Chess_bdt45.svg',
  'bn': 'https://upload.wikimedia.org/wikipedia/commons/e/ef/Chess_ndt45.svg',
  'bp': 'https://upload.wikimedia.org/wikipedia/commons/c/c7/Chess_pdt45.svg',
};

@Component({
  selector: 'app-chessboard',
  standalone: true,
  templateUrl: './chessboard.component.html',
  styleUrl: './chessboard.component.scss'
})
export class ChessboardComponent {
  protected readonly ranks = [8, 7, 6, 5, 4, 3, 2, 1];
  protected readonly files = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];
  
  pieces: ChessPiece[] = [];

  constructor() {
    this.initializeStartingPosition();
  }

  private initializeStartingPosition() {
    const setup = [
      ['br', 'bn', 'bb', 'bq', 'bk', 'bb', 'bn', 'br'], // Rank 8 (y=0)
      ['bp', 'bp', 'bp', 'bp', 'bp', 'bp', 'bp', 'bp'], // Rank 7 (y=1)
      [], [], [], [],                                   // Empty
      ['wp', 'wp', 'wp', 'wp', 'wp', 'wp', 'wp', 'wp'], // Rank 2 (y=6)
      ['wr', 'wn', 'wb', 'wq', 'wk', 'wb', 'wn', 'wr']  // Rank 1 (y=7)
    ];
    
    let idCounter = 0;
    for (let y = 0; y < 8; y++) {
      for (let x = 0; x < 8; x++) {
        if (setup[y] && setup[y][x]) {
          this.pieces.push({
            id: `piece-${idCounter++}`,
            type: setup[y][x],
            x,
            y
          });
        }
      }
    }
  }

  getPieceUrl(type: string): string {
    return PIECE_URLS[type];
  }

  getPieceTransform(piece: ChessPiece): string {
    return `translate(${piece.x * 100}%, ${piece.y * 100}%)`;
  }

  protected flipBoard(): void {
    // Scaffold flip visually by inverting x and y manually for sketchy preview
    this.pieces = this.pieces.map(p => ({
      ...p,
      x: 7 - p.x,
      y: 7 - p.y
    }));
  }

  protected goPreviousMove(): void {}
  protected goNextMove(): void {}
  protected goToGameStart(): void {}
  protected goToGameEnd(): void {}
  protected setPosition(): void {}
  protected clearPosition(): void {
    this.pieces = [];
  }
}
